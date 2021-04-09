using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MissingFeatures;
using Newtonsoft.Json;
using OJS.Workers.Common;
using OJS.Workers.Common.Extensions;
using OJS.Workers.Common.Models;
using OJS.Workers.SubmissionProcessors.Formatters;
using OJS.Workers.SubmissionProcessors.Models;

namespace OJS.Services.Business.Problems
{
    using System.Data.Entity;
    using System.Linq;

    using OJS.Common.Helpers;
    using OJS.Data.Models;
    using OJS.Data.Repositories.Contracts;
    using OJS.Services.Business.ProblemGroups;
    using OJS.Services.Common;
    using OJS.Services.Data.Contests;
    using OJS.Services.Data.ParticipantScores;
    using OJS.Services.Data.ProblemResources;
    using OJS.Services.Data.Problems;
    using OJS.Services.Data.Submissions;
    using OJS.Services.Data.SubmissionsForProcessing;
    using OJS.Services.Data.SubmissionTypes;
    using OJS.Services.Data.TestRuns;
    using OJS.Workers.SubmissionProcessors.Common;

    using IsolationLevel = System.Transactions.IsolationLevel;
    using Resource = Resources.Services.Problems.ProblemsBusiness;
    using SharedResource = Resources.Contests.ContestsGeneral;

    public class ProblemsBusinessService : IProblemsBusinessService
    {
        private readonly IEfDeletableEntityRepository<Problem> problems;
        private readonly IContestsDataService contestsData;
        private readonly IParticipantScoresDataService participantScoresData;
        private readonly IProblemsDataService problemsData;
        private readonly IProblemResourcesDataService problemResourcesData;
        private readonly ISubmissionsDataService submissionsData;
        private readonly ISubmissionsForProcessingDataService submissionsForProcessingData;
        private readonly ITestRunsDataService testRunsData;
        private readonly ISubmissionTypesDataService submissionTypesData;
        private readonly IProblemGroupsBusinessService problemGroupsBusiness;
        private readonly HttpService http;

        public ProblemsBusinessService(
            IEfDeletableEntityRepository<Problem> problems,
            IContestsDataService contestsData,
            IParticipantScoresDataService participantScoresData,
            IProblemsDataService problemsData,
            IProblemResourcesDataService problemResourcesData,
            ISubmissionsDataService submissionsData,
            ISubmissionsForProcessingDataService submissionsForProcessingData,
            ITestRunsDataService testRunsData,
            ISubmissionTypesDataService submissionTypesData,
            IProblemGroupsBusinessService problemGroupsBusiness)
        {
            this.problems = problems;
            this.contestsData = contestsData;
            this.participantScoresData = participantScoresData;
            this.problemsData = problemsData;
            this.problemResourcesData = problemResourcesData;
            this.submissionsData = submissionsData;
            this.submissionsForProcessingData = submissionsForProcessingData;
            this.testRunsData = testRunsData;
            this.submissionTypesData = submissionTypesData;
            this.problemGroupsBusiness = problemGroupsBusiness;
            this.http = new HttpService();
        }

        public void RetestById(int id)
        {
            var allSubmissionIds = this.submissionsData.GetIdsByProblem(id).ToList();
            List<object> allSubmissionsRequestBodyData = new List<object>();
            using (var scope = TransactionsHelper.CreateTransactionScope(IsolationLevel.RepeatableRead))
            {
                this.participantScoresData.DeleteAllByProblem(id);

                this.submissionsData.SetAllToUnprocessedByProblem(id);
                
                // this.submissionsForProcessingData.AddOrUpdateBySubmissionIds(allSubmissionIds);

                var submissions = this.submissionsData.GetAllByProblem(id)
                    .Include("Problem")
                    .Include("SubmissionType")
                    .Include("Problem.Checker")
                    .ToList();
                
                var formatterFactory = new FormatterServiceFactory();
                allSubmissionsRequestBodyData = submissions
                    .Select(s =>
                    {
                        this.testRunsData.DeleteBySubmission(s.Id);
                        return this.BuildDistributorSubmissionBody(s, formatterFactory);
                    })
                    .ToList();
                
                scope.Complete();
            }

            string toJson = Newtonsoft.Json.JsonConvert.SerializeObject(allSubmissionsRequestBodyData);

            var tasks = allSubmissionsRequestBodyData.Select<object, Task>(i => Task.Run(() => DoWorkAsync(i)));

            Task.WhenAll(tasks);
        }

        private async Task<string> DoWorkAsync(object requestBody)
        {
            var distributorEndpoint = $"http://localhost:6000/submissions/add";
            return await this.http.PostJsonAsync<object>(distributorEndpoint, requestBody);
        }

        private object BuildDistributorSubmissionBody(Submission submission, FormatterServiceFactory formatterServicesFactory)
        {
            var submissionType = ExecutionType.TestsExecution.ToString().ToHyphenSeparatedWords();

            var executionStrategy = formatterServicesFactory.Get<ExecutionStrategyType>().Format(submission.SubmissionType.ExecutionStrategyType);
            var fileContent = string.IsNullOrEmpty(submission.ContentAsString)
                ? submission.Content
                : null;
            var code = submission.ContentAsString ?? string.Empty;
            var checkerType = formatterServicesFactory.Get<string>().Format(submission.Problem.Checker.ClassName);
            var tests = submission.Problem.Tests.Select(t => new
            {
                t.Id,
                Input = t.InputData,
                Output = t.OutputDataAsString,
                t.IsTrialTest,
                t.OrderBy
            }).ToList();

            var submissionRequestBody = new
            {
                Id = submission.Id,
                ExecutionType = submissionType,
                ExecutionStrategy = executionStrategy,
                FileContent = fileContent,
                Code = code,
                submission.Problem.TimeLimit,
                submission.Problem.MemoryLimit,
                ExecutionDetails = new
                {
                    MaxPoints = submission.Problem.MaximumPoints,
                    CheckerType = checkerType,
                    Tests = tests,
                    submission.Problem.SolutionSkeleton
                },
            };

            return submissionRequestBody;
        }

        public void DeleteById(int id)
        {
            var problem = this.problemsData
                .GetByIdQuery(id)
                .Select(p => new
                {
                    p.ProblemGroupId,
                    p.ProblemGroup.ContestId
                })
                .FirstOrDefault();

            if (problem == null)
            {
                return;
            }

            using (var scope = TransactionsHelper.CreateTransactionScope(IsolationLevel.RepeatableRead))
            {
                this.testRunsData.DeleteByProblem(id);

                this.problemResourcesData.DeleteByProblem(id);

                this.submissionsData.DeleteByProblem(id);

                this.problems.Delete(id);
                this.problems.SaveChanges();

                if (!this.contestsData.IsOnlineById(problem.ContestId))
                {
                    this.problemGroupsBusiness.DeleteById(problem.ProblemGroupId);
                }

                scope.Complete();
            }
        }

        public void DeleteByContest(int contestId) =>
            this.problemsData
                .GetAllByContest(contestId)
                .Select(p => p.Id)
                .ToList()
                .ForEach(this.DeleteById);

        public ServiceResult CopyToContestByIdByContestAndProblemGroup(int id, int contestId, int? problemGroupId)
        {
            var problem = this.problemsData
                .GetByIdQuery(id)
                .AsNoTracking()
                .Include(p => p.Tests)
                .Include(p => p.Resources)
                .SingleOrDefault();

            if (problem?.ProblemGroup.ContestId == contestId)
            {
                return new ServiceResult(Resource.Cannot_copy_problems_into_same_contest);
            }

            if (!this.contestsData.ExistsById(contestId))
            {
                return new ServiceResult(SharedResource.Contest_not_found);
            }

            if (this.contestsData.IsActiveById(contestId))
            {
                return new ServiceResult(Resource.Cannot_copy_problems_into_active_contest);
            }
            
            this.CopyProblemToContest(problem, contestId, problemGroupId);

            return ServiceResult.Success;
        }

        private void CopyProblemToContest(Problem problem, int contestId, int? problemGroupId)
        {
            int orderBy;

            if (problem == null)
            {
                return;
            }

            if (problemGroupId.HasValue)
            {
                orderBy = this.problemsData.GetNewOrderByProblemGroup(problemGroupId.Value);

                problem.ProblemGroup = null;
                problem.ProblemGroupId = problemGroupId.Value;
            }
            else
            {
                orderBy = this.problemsData.GetNewOrderByContest(contestId);

                problem.ProblemGroup = new ProblemGroup
                {
                    ContestId = contestId,
                    OrderBy = orderBy
                };
            }

            problem.OrderBy = orderBy;
            problem.SubmissionTypes = this.submissionTypesData.GetAllByProblem(problem.Id).ToList();

            this.problemsData.Add(problem);
        }
    }
}