using System;
using System.Net;
using System.Web;
using OJS.Data.Models;
using OJS.Services.Data.SubmissionsForProcessing;
using OJS.Web.Models;
using OJS.Workers.Common.Models;

namespace OJS.Web.Controllers
{
    using System.Diagnostics;
    using System.Linq;
    using System.Web.Mvc;

    using Kendo.Mvc.Extensions;
    using Kendo.Mvc.UI;

    using Newtonsoft.Json;

    using OJS.Common;
    using OJS.Common.Models;
    using OJS.Data;
    using OJS.Services.Data.Submissions;
    using OJS.Web.Common.Attributes;
    using OJS.Web.Common.Extensions;
    using OJS.Web.ViewModels.Submission;
    using OJS.Workers.Common;

    public class SubmissionsController : BaseController
    {
        private readonly ISubmissionsDataService submissionsData;
        private readonly ISubmissionsForProcessingDataService submissionsForProcessingDataService;


        public SubmissionsController(
            IOjsData data,
            ISubmissionsDataService submissionsData,
            ISubmissionsForProcessingDataService submissionsForProcessingDataService)
            : base(data)
        {
            this.submissionsData = submissionsData;
            this.submissionsForProcessingDataService = submissionsForProcessingDataService;
        }

        [AuthorizeRoles(SystemRole.Administrator, SystemRole.Lecturer)]
        public ActionResult Index()
        {
            if (this.User.IsAdmin())
            {
                this.ViewBag.SubmissionsInQueue = this.Data.Submissions.All().Count(x => !x.Processed);
            }

            return this.View("AdvancedSubmissions");
        }

        [AuthorizeRoles(SystemRole.Administrator, SystemRole.Lecturer)]
        public ActionResult GetSubmissionsGrid(
            bool notProcessedOnly = false,
            string userId = null,
            int? contestId = null)
        {
            var filter = new SubmissionsFilterViewModel
            {
                ContestId = contestId,
                UserId = userId,
                NotProcessedOnly = this.User.IsAdmin() && notProcessedOnly
            };

            return this.PartialView("_AdvancedSubmissionsGridPartial", filter);
        }

        [HttpPost]
        [Authorize]
        public ActionResult ReadSubmissions(
            [DataSourceRequest]DataSourceRequest request,
            string userId,
            bool notProcessedOnly = false,
            int? contestId = null)
        {
            var data = this.submissionsData.GetAll();

            var userIsAdmin = this.User.IsAdmin();
            var userIsLecturer = this.User.IsLecturer();

            if (userIsLecturer && !userIsAdmin)
            {
                data = this.submissionsData.GetAllFromContestsByLecturer(this.UserProfile.Id);
            }

            // UserId filter is available only for administrators and lecturers
            if (!userIsAdmin && !userIsLecturer)
            {
                userId = this.UserProfile.Id;
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                data = data.Where(s => s.Participant.UserId == userId);
            }

            // NotProcessedOnly filter is available only for administrators
            if (userIsAdmin && notProcessedOnly)
            {
                data = data.Where(s => !s.Processed);
            }

            if (contestId.HasValue)
            {
                data = data.Where(s => s.Problem.ProblemGroup.ContestId == contestId.Value);
            }

            var result = data.Select(SubmissionViewModel.FromSubmission);

            var serializationSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
            var json = JsonConvert.SerializeObject(result.ToDataSourceResult(request), Formatting.None, serializationSettings);
            return this.Content(json, GlobalConstants.JsonMimeType);
        }

        [HttpPost]
        [Authorize(Roles = GlobalConstants.AdministratorRoleName)]
        [ValidateAntiForgeryToken]
        public ActionResult StartOjsLocalWorkerService()
        {
            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                ErrorDialog = false,
                FileName = "cmd",
                Arguments = @"/c SCHTASKS.EXE /RUN /TN JudgeTasks\RestartService"
            };

            using (var cmdProcess = Process.Start(processStartInfo))
            {
                if (cmdProcess == null)
                {
                    this.TempData.AddDangerMessage("Couldn't start process.");
                }
                else
                {
                    cmdProcess.StartInfo = processStartInfo;
                    cmdProcess.Start();
                    cmdProcess.WaitForExit(Constants.DefaultProcessExitTimeOutMilliseconds);

                    var error = cmdProcess.StandardError.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        this.TempData.AddInfoMessage("The service was started successfully!");
                    }
                    else
                    {
                        this.TempData.AddDangerMessage("The service is already running or an error has occurred while starting it.");
                    }
                }
            }

            return this.RedirectToAction<SubmissionsController>(c => c.Index());
        }
        
        [HttpPost]
        public ActionResult SaveExecutionResult(SubmissionExecutionResult submissionExecutionResult)
        {
            var submission = this.submissionsData.GetById(submissionExecutionResult.SubmissionId);
            Console.WriteLine();

            submission.IsCompiledSuccessfully = submissionExecutionResult.ExecutionResult.IsCompiledSuccessfully;
            submission.CompilerComment = submissionExecutionResult.ExecutionResult.CompilerComment;
            submission.Points = submissionExecutionResult.ExecutionResult.TaskResult.Points;
            submission.Processed = true;

            if (!submissionExecutionResult.ExecutionResult.IsCompiledSuccessfully)
            {
                throw new HttpException((int)HttpStatusCode.NotFound, "Submission not compiled successfully");
            }

            foreach (var testResult in submissionExecutionResult.ExecutionResult.TaskResult.TestResults)
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

            var submissionForProcessing = this.submissionsForProcessingDataService.GetBySubmission(submission.Id);
            this.submissionsForProcessingDataService.RemoveBySubmission(submission.Id);

            return this.JsonSuccess(submissionExecutionResult.SubmissionId);
        }
    }
}