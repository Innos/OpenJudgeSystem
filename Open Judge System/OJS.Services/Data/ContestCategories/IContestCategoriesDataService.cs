﻿namespace OJS.Services.Data.ContestCategories
{
    using System.Linq;

    using OJS.Data.Models;
    using OJS.Services.Common;

    public interface IContestCategoriesDataService : IService
    {
        IQueryable<ContestCategory> GetAllVisible();

        IQueryable<ContestCategory> GetVisibleByIdQuery(int id);

        IQueryable<ContestCategory> GetAllVisibleByLecturer(string lecturerId);

        string GetNameById(int id);

        bool HasContestsById(int id);
    }
}