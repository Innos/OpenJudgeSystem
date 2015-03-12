﻿namespace OJS.Web.Areas.Contests.ViewModels.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class ParticipantFullResultViewModel
    {
        public string ParticipantUsername { get; set; }

        public string ParticipantFirstName { get; set; }

        public string ParticipantLastName { get; set; }

        public string ParticipantFullName
        {
            get
            {
                return string.Format("{0} {1}", this.ParticipantFirstName, this.ParticipantLastName).Trim();
            }
        }

        public IEnumerable<ProblemFullResultViewModel> ProblemResults { get; set; }

        public int Total
        {
            get
            {
                var totalPoints = 0;
                foreach (var problemResult in this.ProblemResults)
                {
                    if (problemResult.BestSubmission != null)
                    {
                        totalPoints += problemResult.BestSubmission.Points;
                    }
                }

                return totalPoints;
            }
        }

        public double? GetContestTimeInSeconds(DateTime? contestStartTime)
        {
            if (contestStartTime.HasValue)
            {
                var lastSubmission = this.ProblemResults
                    .Where(x => x.BestSubmission != null)
                    .OrderByDescending(x => x.BestSubmission.CreatedOn)
                    .Select(x => x.BestSubmission)
                    .FirstOrDefault();

                if (lastSubmission != null)
                {
                    var lastSubmissionTime = lastSubmission.CreatedOn;
                    var contestTimeInSeconds = (lastSubmissionTime - contestStartTime.Value).TotalSeconds;
                    return contestTimeInSeconds;
                }
            }

            return null;
        }
    }
}
