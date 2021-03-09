using System;
using OJS.Data.Models;
using OJS.Workers.Common.Models;
using OJS.Workers.SubmissionProcessors;
using OJS.Workers.SubmissionProcessors.Models;

namespace OJS.Web.Areas.Contests.Controllers
{
    using System.Linq;
    using System.Net;
    using System.Web;
    using System.Web.Mvc;

    using OJS.Common;
    using OJS.Data;
    using OJS.Services.Data.Contests;
    using OJS.Services.Data.Problems;
    using OJS.Services.Data.Submissions;
    using OJS.Web.Areas.Contests.ViewModels.Submissions;
    using OJS.Web.Common.Extensions;
    using OJS.Web.Controllers;

    using Resource = Resources.Areas.Contests.ContestsGeneral;

    public class SubmissionsController : BaseController
    {
        private readonly ISubmissionsDataService submissionsData;
        private readonly IProblemsDataService problemsData;
        private readonly IContestsDataService contestsData;

        public SubmissionsController(
            IOjsData data,
            ISubmissionsDataService submissionsData,
            IProblemsDataService problemsData,
            IContestsDataService contestsData)
            : base(data)
        {
            this.submissionsData = submissionsData;
            this.problemsData = problemsData;
            this.contestsData = contestsData;
        }

        [ActionName("View")]
        [Authorize]
        public ActionResult Details(int id)
        {
            var submission = this.submissionsData
                .GetByIdQuery(id)
                .Select(SubmissionDetailsViewModel.FromSubmission)
                .FirstOrDefault();

            if (submission == null)
            {
                throw new HttpException((int)HttpStatusCode.NotFound, Resource.Submission_not_found);
            }

            var userHasAdminPermissions = this.CheckIfUserHasProblemPermissions(submission.ProblemId ?? 0);

            if (!userHasAdminPermissions && submission.IsDeleted)
            {
                throw new HttpException((int)HttpStatusCode.NotFound, Resource.Submission_not_found);
            }

            if (!userHasAdminPermissions && this.UserProfile != null && submission.UserId != this.UserProfile.Id)
            {
                throw new HttpException((int)HttpStatusCode.Forbidden, Resource.Submission_not_made_by_user);
            }

            var isFromActiveContest = this.contestsData.IsActiveById(submission.ContestId);
            var problemsInContest = this.problemsData.GetAllByContest(submission.ContestId);

            if (isFromActiveContest &&
                this.contestsData.IsOnlineById(submission.ContestId) &&
                !userHasAdminPermissions)
            {
                problemsInContest = problemsInContest
                    .Where(p => p.Participants.Any(par => par.UserId == submission.UserId));
            }

            submission.ProblemIndexInContest = problemsInContest
                .OrderBy(p => p.ProblemGroup.OrderBy)
                .ThenBy(p => p.OrderBy)
                .ThenBy(p => p.Name)
                .Select(p => p.Id)
                .ToList()
                .IndexOf(submission.ProblemId.Value);

            submission.IsContestActive = isFromActiveContest;
            submission.UserHasAdminPermission = userHasAdminPermissions;

            return this.View(submission);
        }

        // TODO: Extract common validations between Download() and Details()
        [Authorize]
        public FileResult Download(int id)
        {
            var submission = this.Data.Submissions
                .All()
                .Where(x => x.Id == id)
                .Select(SubmissionDetailsViewModel.FromSubmission)
                .FirstOrDefault();

            if (submission == null || (submission.IsDeleted && !this.User.IsAdmin()))
            {
                throw new HttpException((int)HttpStatusCode.NotFound, Resource.Submission_not_found);
            }

            var userHasRights = submission.UserId == this.UserProfile.Id || this.CheckIfUserHasProblemPermissions(submission.ProblemId ?? 0);
            if (!userHasRights)
            {
                throw new HttpException((int)HttpStatusCode.Forbidden, Resource.Submission_not_made_by_user);
            }

            // TODO: When text content is saved, uncompressing should be performed
            return this.File(submission.Content, GlobalConstants.BinaryFileMimeType, string.Format("Submission_{0}.{1}", submission.Id, submission.FileExtension));
        }

        [HttpPost]
        public ActionResult SaveExecutionResult(int id, RemoteSubmissionResult submissionResult)
        {
            var submission = this.submissionsData.GetById(id);

            if (submission == null || (submission.IsDeleted && !this.User.IsAdmin()))
            {
                throw new HttpException((int)HttpStatusCode.NotFound, Resource.Submission_not_found);
            }
            
            this.UpdateSubmissionResult(submission, submissionResult);

            return this.JsonSuccess(submission.Id);
        }

        private void UpdateSubmissionResult(Submission submission, RemoteSubmissionResult result)
        {
            submission.IsCompiledSuccessfully = result.ExecutionResult.IsCompiledSuccessfully;
            submission.CompilerComment = result.ExecutionResult.CompilerComment;

            if (!result.ExecutionResult.IsCompiledSuccessfully)
            {
                this.submissionsData.Update(submission);
                return;
            }

            foreach (var testResult in result.ExecutionResult.TaskResult.TestResults)
            {
                var testRun = new TestRun
                {
                    CheckerComment = testResult.CheckerDetails.Comment,
                    ExpectedOutputFragment = testResult.CheckerDetails.ExpectedOutputFragment,
                    UserOutputFragment = testResult.CheckerDetails.UserOutputFragment,
                    ExecutionComment = testResult.ExecutionComment,
                    MemoryUsed = testResult.MemoryUsed,
                    ResultType = (TestRunResultType)Enum.Parse(typeof(TestRunResultType), testResult.ResultType),
                    TestId = testResult.Id,
                    TimeUsed = testResult.TimeUsed
                };

                submission.TestRuns.Add(testRun);
            }

            this.submissionsData.Update(submission);
        }
    }
}