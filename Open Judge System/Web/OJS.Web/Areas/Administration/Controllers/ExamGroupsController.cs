﻿namespace OJS.Web.Areas.Administration.Controllers
{
    using System.Collections;
    using System.Data.Entity;
    using System.Linq;
    using System.Web.Mvc;

    using Kendo.Mvc.Extensions;
    using Kendo.Mvc.UI;

    using OJS.Data;
    using OJS.Data.Models;
    using OJS.Services.Data.Contests;
    using OJS.Services.Data.ExamGroups;
    using OJS.Services.Data.Users;
    using OJS.Web.Areas.Administration.Controllers.Common;
    using OJS.Web.Common.Extensions;

    using DetailModelType = OJS.Web.Areas.Administration.ViewModels.User.UserProfileSimpleAdministrationViewModel;
    using GeneralResource = Resources.Areas.Administration.AdministrationGeneral;
    using Resource = Resources.Areas.Administration.ExamGroups.ExamGroupsController;
    using ViewModelType = OJS.Web.Areas.Administration.ViewModels.ExamGroups.ExamGroupAdministrationViewModel;

    public class ExamGroupsController : LecturerBaseGridController
    {
        private const int DefaultUsersTakeCount = 20;
        private const int DefaultContestsToTake = 15;

        private readonly IExamGroupsDataService examGroupsData;
        private readonly IUsersDataService usersData;
        private readonly IContestsDataService contestsData;

        public ExamGroupsController(
            IOjsData data,
            IExamGroupsDataService examGroupsData,
            IUsersDataService usersData,
            IContestsDataService contestsData)
            : base(data)
        {
            this.examGroupsData = examGroupsData;
            this.usersData = usersData;
            this.contestsData = contestsData;
        }

        public ActionResult Index() => this.View();

        public override IEnumerable GetData()
        {
            var examGroups = this.examGroupsData.GetAll();

            if (this.User.IsLecturer())
            {
                examGroups = examGroups.Where(eg => eg.Contest == null ||
                    (eg.Contest.Lecturers.Any(l => l.LecturerId == this.UserProfile.Id) ||
                        eg.Contest.Category.Lecturers.Any(l => l.LecturerId == this.UserProfile.Id)));
            }

            return examGroups.Select(ViewModelType.FromExamGroup);
        }

        public override object GetById(object id) => this.GetByIdAsNoTracking((int)id);

        [HttpPost]
        public ActionResult Create([DataSourceRequest]DataSourceRequest request, ViewModelType model)
        {
            var contestId = model.ContestId.HasValue
                ? this.contestsData.GetByIdQuery(model.ContestId.Value).Select(c => c.Id).FirstOrDefault()
                : default(int);

            if (contestId != default(int))
            {
                if (!this.UserHasContestRights(contestId))
                {
                    this.ModelState.AddModelError(nameof(model.ContestId), Resource.Cannot_attach_contest);
                    return this.GridOperation(request, model);
                }

                this.BaseCreate(model.GetEntityModel());
            }
            else
            {
                this.ModelState.AddModelError(nameof(model.ContestId), string.Empty);
            }
            
            return this.GridOperation(request, model);
        }

        [HttpPost]
        public ActionResult Update([DataSourceRequest]DataSourceRequest request, ViewModelType model)
        {
            if (!model.Id.HasValue)
            {
                return this.GridOperation(request, model);
            }

            var entity = this.GetByIdAsNoTracking(model.Id.Value);

            var examGroup = model.GetEntityModel(entity);

            if (examGroup.ContestId.HasValue)
            {
                if (!this.contestsData
                    .GetAll()
                    .Any(c => c.Id == examGroup.ContestId.Value))
                {
                    this.ModelState.AddModelError(nameof(model.ContestId), string.Empty);
                    return this.GridOperation(request, model);
                }

                if (!this.UserHasContestRights(examGroup.ContestId.Value))
                {
                    this.ModelState.AddModelError(nameof(model.ContestId), Resource.Cannot_attach_contest);
                    return this.GridOperation(request, model);
                }
            }

            this.BaseUpdate(examGroup);

            return this.GridOperation(request, model);
        }

        [HttpPost]
        public ActionResult Destroy([DataSourceRequest]DataSourceRequest request, ViewModelType model)
        {
            if (model.ContestId != null)
            {
                if (!this.UserHasContestRights(model.ContestId.Value))
                {
                    this.ModelState.AddModelError(string.Empty, GeneralResource.No_privileges_message);
                    return this.GridOperation(request, model);
                }

                if (this.contestsData.IsActiveById(model.ContestId.Value))
                {
                    this.ModelState.AddModelError(string.Empty, Resource.Cannot_delete_group_with_active_contest);
                    return this.GridOperation(request, model);
                }
            }

            this.BaseDestroy(model.Id);
            return this.GridOperation(request, model);
        }

        [HttpPost]
        public JsonResult UsersInExamGroup([DataSourceRequest]DataSourceRequest request, int id)
        {
            var users = this.examGroupsData
                .GetUsersByIdQuery(id)
                .Select(DetailModelType.FromUserProfile);

            return this.Json(users.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult RemoveUserFromExamGroup(
            [DataSourceRequest]DataSourceRequest request,
            DetailModelType model,
            int id)
        {
            var contestId = this.examGroupsData
                .GetByIdQuery(id)
                .Select(eg => eg.ContestId)
                .FirstOrDefault();

            if (!contestId.HasValue)
            {
               this.ModelState.AddModelError(string.Empty, Resource.Cannot_remove_users);
                return this.RedirectToAction<ExamGroupsController>(c => c.Index());
            }

            if (!this.UserHasContestRights(contestId.Value))
            {
                this.TempData.AddDangerMessage(GeneralResource.No_privileges_message);
                return this.RedirectToAction<ExamGroupsController>(c => c.Index());
            }

            this.examGroupsData.RemoveUserByIdAndUser(id, model.UserId);
            return this.GridOperation(request, model);
        }

        [HttpPost]
        public ActionResult AddUserToExamGroup(
            [DataSourceRequest]DataSourceRequest request,
            int id,
            string userId)
        {
            var examGroup = this.examGroupsData.GetById(id);
            var user = this.usersData.GetById(userId);

            if (examGroup.ContestId == null)
            {
                this.ModelState.AddModelError(string.Empty, Resource.Cannot_add_users);
                return this.GridOperation(request, null);
            }

            if (!this.UserHasContestRights(examGroup.ContestId.Value))
            {
                this.ModelState.AddModelError(string.Empty, GeneralResource.No_privileges_message);
                return this.GridOperation(request, null);
            }

            examGroup.Users.Add(user);
            this.examGroupsData.Update(examGroup);

            var result = new DetailModelType
            {
                UserId = user.Id,
                Username = user.UserName,
                FirstName = user.UserSettings.FirstName,
                LastName = user.UserSettings.LastName,
                Email = user.Email
            };

            return this.Json(new[] { result }.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetAvailableUsers(string userFilter)
        {
            var users = this.usersData.GetAll().Take(DefaultUsersTakeCount);

            if (!string.IsNullOrWhiteSpace(userFilter))
            {
                users = this.usersData
                    .GetAll()
                    .Where(u => u.UserName.ToLower().Contains(userFilter.ToLower()))
                    .Take(DefaultUsersTakeCount);
            }

            var result = users
                .Select(u => new SelectListItem
                {
                    Text = u.UserName,
                    Value = u.Id
                })
                .ToList();

            return this.Json(result, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetAvailableContests(string contestFilter)
        {
            var contests = this.contestsData
                .GetAll()
                .OrderByDescending(c => c.CreatedOn);

            if (!this.User.IsAdmin() && this.User.IsLecturer())
            {
                contests = contests
                    .Where(c =>
                        c.Lecturers.Any(l => l.LecturerId == this.UserProfile.Id) ||
                        c.Category.Lecturers.Any(l => l.LecturerId == this.UserProfile.Id))
                    .OrderByDescending(c => c.CreatedOn);
            }

            if (!string.IsNullOrWhiteSpace(contestFilter))
            {
                contests = contests
                    .Where(c => c.Name.Contains(contestFilter))
                    .Take(DefaultContestsToTake)
                    .OrderByDescending(c => c.CreatedOn);
            }

            var result = contests
                .Select(c => new
                {
                    c.Name,
                    c.Id
                })
                .ToList();

            return this.Json(result, JsonRequestBehavior.AllowGet);
        }

        public override string GetEntityKeyName() => this.GetEntityKeyNameByType(typeof(ExamGroup));

        private ExamGroup GetByIdAsNoTracking(int id) =>
            this.examGroupsData
                .GetByIdQuery(id)
                .AsNoTracking()
                .FirstOrDefault();

        private bool UserHasContestRights(int contestId) =>
            this.User.IsAdmin() ||
            this.contestsData.IsUserLecturerInByContestAndUser(contestId, this.UserProfile.Id);
    }
}