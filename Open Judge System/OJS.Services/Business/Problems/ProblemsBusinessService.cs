﻿namespace OJS.Services.Business.Problems
{
    using System.Linq;

    using OJS.Common.Helpers;
    using OJS.Data.Models;
    using OJS.Data.Repositories.Contracts;
    using OJS.Services.Business.ProblemGroups;
    using OJS.Services.Data.Contests;
    using OJS.Services.Data.ParticipantScores;
    using OJS.Services.Data.ProblemResources;
    using OJS.Services.Data.Problems;
    using OJS.Services.Data.Submissions;
    using OJS.Services.Data.SubmissionsForProcessing;
    using OJS.Services.Data.TestRuns;

    using IsolationLevel = System.Transactions.IsolationLevel;

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
        private readonly IProblemGroupsBusinessService problemGroupsBusiness;

        public ProblemsBusinessService(
            IEfDeletableEntityRepository<Problem> problems,
            IContestsDataService contestsData,
            IParticipantScoresDataService participantScoresData,
            IProblemsDataService problemsData,
            IProblemResourcesDataService problemResourcesData,
            ISubmissionsDataService submissionsData,
            ISubmissionsForProcessingDataService submissionsForProcessingData,
            ITestRunsDataService testRunsData,
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
            this.problemGroupsBusiness = problemGroupsBusiness;
        }

        public void RetestById(int id)
        {
            var submissionIds = this.submissionsData.GetIdsByProblem(id).ToList();

            using (var scope = TransactionsHelper.CreateTransactionScope(IsolationLevel.RepeatableRead))
            {
                this.participantScoresData.DeleteAllByProblem(id);

                this.submissionsData.SetAllToUnprocessedByProblem(id);

                this.submissionsForProcessingData.AddOrUpdateBySubmissionIds(submissionIds);

                scope.Complete();
            }
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
                    this.problemGroupsBusiness.DeleteById(problem.ProblemGroupId.Value);
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
    }
}