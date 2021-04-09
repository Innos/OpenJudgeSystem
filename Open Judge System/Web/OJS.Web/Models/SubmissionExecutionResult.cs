using System.Collections.Generic;

namespace OJS.Web.Models
{
    public class SubmissionExecutionResult
    {
        public int SubmissionId { get; set; }
        public ExceptionModel Exception { get; set; }
        public ExecutionResultResponseModel ExecutionResult { get; set; }
    }

    public class ExceptionModel
    {
        public string Message { get; set; }

        public string StackTrace { get; set; }
    }

    public class ExecutionResultResponseModel
    {
        public string Id { get; set; }

        public bool IsCompiledSuccessfully { get; set; }

        public string CompilerComment { get; set; }

        public OutputResultResponseModel OutputResult { get; set; }

        public TaskResultResponseModel TaskResult { get; set; }
    }

    public class OutputResultResponseModel
    {
        public int TimeUsedInMs { get; set; }

        public int MemoryUsedInBytes { get; set; }

        public string ResultType { get; set; }

        public string Output { get; set; }
    }

    public class TaskResultResponseModel
    {
        public int Points { get; set; }

        public IEnumerable<TestResultResponseModel> TestResults { get; set; }
    }

    public class TestResultResponseModel
    {
        public int Id { get; set; }

        public string ResultType { get; set; }

        public string ExecutionComment { get; set; }

        public string Output { get; set; }

        public CheckerDetailsResponseModel CheckerDetails { get; set; }

        public int TimeUsed { get; set; }

        public int MemoryUsed { get; set; }
    }

    public class CheckerDetailsResponseModel
    {
        public string Comment { get; set; }

        public string ExpectedOutputFragment { get; set; }

        public string UserOutputFragment { get; set; }
    }
}