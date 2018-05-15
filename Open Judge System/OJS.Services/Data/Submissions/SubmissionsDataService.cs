﻿namespace OJS.Services.Data.Submissions
{
    using System.Collections.Generic;
    using System.Linq;

    using EntityFramework.Extensions;

    using OJS.Common.Helpers;
    using OJS.Data.Archives;
    using OJS.Data.Models;
    using OJS.Data.Repositories.Contracts;

    public class SubmissionsDataService : ISubmissionsDataService
    {
        private readonly IEfDeletableEntityRepository<Submission> submissions;
        private readonly IArchivesDbContext archivesContext;

        public SubmissionsDataService(
            IEfDeletableEntityRepository<Submission> submissions,
            IArchivesDbContext archivesContext)
        {
            this.submissions = submissions;
            this.archivesContext = archivesContext;
        }

        public Submission GetBestForParticipantByProblem(int participantId, int problemId) =>
            this.GetAllByProblemAndParticipant(problemId, participantId)
                .Where(s => s.Processed)
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();

        public IQueryable<Submission> GetByIdQuery(int id) =>
            this.submissions.All().Where(s => s.Id == id);

        public IQueryable<Submission> GetAllByProblem(int problemId) =>
            this.submissions.All().Where(s => s.ProblemId == problemId);

        public IQueryable<Submission> GetAllByProblemAndParticipant(int problemId, int participantId) =>
            this.GetAllByProblem(problemId).Where(s => s.ParticipantId == participantId);

        public IEnumerable<int> GetIdsByProblem(int problemId) =>
            this.GetAllByProblem(problemId).Select(s => s.Id);

        public void SetAllToUnprocessedByProblem(int problemId) =>
            this.GetAllByProblem(problemId).Update(s => new Submission{ Processed = false });

        public void DeleteByProblem(int problemId) =>
            this.submissions.Delete(s => s.ProblemId == problemId);

        public void ArchiveById(int id)
        {
            var submission = this.GetByIdQuery(id).SingleOrDefault();

            if (submission == null)
            {
                return;
            }

            using (var scope = TransactionsHelper.CreateTransactionScope())
            {
                this.archivesContext.Submissions.Add(new ArchivedSubmission(submission));
                this.archivesContext.DbContext.SaveChanges();

                this.submissions.HardDelete(submission);

                scope.Complete();
            }
        }
    }
}