﻿namespace OJS.Web.Areas.Administration.Controllers
{
    using System.Collections;
    using System.Data.Entity;
    using System.Linq;
    using System.Web.Mvc;
    using System.Web.Mvc.Expressions;

    using Kendo.Mvc.Extensions;
    using Kendo.Mvc.UI;

    using OJS.Common;
    using OJS.Common.Extensions;
    using OJS.Common.Models;
    using OJS.Data;
    using OJS.Data.Models;
    using OJS.Services.Business.ProblemGroups;
    using OJS.Services.Data.Contests;
    using OJS.Services.Data.ProblemGroups;
    using OJS.Web.Areas.Administration.Controllers.Common;
    using OJS.Web.Common.Extensions;
    using OJS.Web.ViewModels.Common;

    using DetailViewModelType = OJS.Web.Areas.Administration.ViewModels.Problem.ProblemViewModel;
    using GeneralResource = Resources.Areas.Administration.AdministrationGeneral;
    using Resource = Resources.Areas.Administration.ProblemGroups.ProblemGroupsControllers;
    using ViewModelType = OJS.Web.Areas.Administration.ViewModels.ProblemGroup.ProblemGroupAdministrationViewModel;

    [RouteArea(GlobalConstants.AdministrationAreaName, AreaPrefix = GlobalConstants.AdministrationAreaName)]
    [RoutePrefix("ProblemGroups")]
    public class ProblemGroupsController : LecturerBaseGridController
    {
        private readonly IProblemGroupsDataService problemGroupsData;
        private readonly IProblemGroupsBusinessService problemGroupsBusiness;
        private readonly IContestsDataService contestsData;

        public ProblemGroupsController(
            IOjsData data,
            IProblemGroupsDataService problemGroupsData,
            IProblemGroupsBusinessService problemGroupsBusiness,
            IContestsDataService contestsData)
            : base(data)
        {
            this.problemGroupsData = problemGroupsData;
            this.problemGroupsBusiness = problemGroupsBusiness;
            this.contestsData = contestsData;
        }

        public override IEnumerable GetData() =>
            this.problemGroupsData
                .GetAll()
                .Select(ViewModelType.FromProblemGroup);

        public override object GetById(object id) => this.GetByIdAsNoTracking((int)id);

        public override string GetEntityKeyName() => this.GetEntityKeyNameByType(typeof(ProblemGroup));

        public ActionResult Index()
        {
            this.PrepareViewBagData();
            return this.View();
        }

        [Route("Contest/{contestId:int}")]
        public ActionResult Index(int contestId)
        {
            if (!this.CheckIfUserHasContestPermissions(contestId, this.contestsData))
            {
                return this.RedirectToContestsAdminPanelWithNoPrivilegesMessage();
            }

            this.ViewBag.ContestId = contestId;

            this.PrepareViewBagData();
            return this.View();
        }

        [HttpPost]
        public ActionResult Create(
            [DataSourceRequest] DataSourceRequest request,
            ViewModelType model)
        {
            if (!this.IsModelAndContestValid(model))
            {
                return this.GridOperation(request, model);
            }

            if (!this.contestsData.IsOnlineById(model.ContestId))
            {
                this.ModelState.AddModelError(
                    string.Empty,
                    string.Format(Resource.Can_create_only_in_online_contest, nameof(ContestType.OnlinePracticalExam)));

                return this.GridOperation(request, model);
            }

            if (this.contestsData.IsActiveById(model.ContestId))
            {
                this.ModelState.AddModelError(string.Empty, Resource.Active_contest_cannot_add_problem_group);
                return this.GridOperation(request, model);
            }

            var problemGroup = model.GetEntityModel();
            problemGroup.Type = model.Type.GetValidTypeOrNull();

            model.Id = (int)this.BaseCreate(problemGroup);
            return this.GridOperation(request, model);
        }

        [HttpPost]
        public ActionResult Update([DataSourceRequest] DataSourceRequest request, ViewModelType model)
        {
            var existingProblemGroup = this.GetByIdAsNoTracking(model.Id);

            if (existingProblemGroup == null || !this.IsModelAndContestValid(model))
            {
                return this.GridOperation(request, model);
            }

            if (existingProblemGroup.OrderBy != model.OrderBy &&
                !this.contestsData.IsOnlineById(model.ContestId))
            {
                this.ModelState.AddModelError(
                    string.Empty,
                    string.Format(
                        Resource.Can_edit_orderby_only_in_online_contest,
                        ContestType.OnlinePracticalExam.GetDescription()));

                return this.GridOperation(request, model);
            }

            var newProblemGroup = model.GetEntityModel(existingProblemGroup);
            newProblemGroup.Type = model.Type.GetValidTypeOrNull();

            this.BaseUpdate(newProblemGroup);
            return this.GridOperation(request, model);
        }

        [HttpPost]
        public ActionResult Destroy([DataSourceRequest] DataSourceRequest request, ViewModelType model)
        {
            if (!this.IsModelAndContestValid(model))
            {
                return this.GridOperation(request, model);
            }

            if (this.contestsData.IsActiveById(model.ContestId))
            {
                this.ModelState.AddModelError(string.Empty, Resource.Active_contest_cannot_delete_problem_group);
                return this.GridOperation(request, model);
            }

            var result = this.problemGroupsBusiness.DeleteById(model.Id);

            if (result.IsError)
            {
                this.ModelState.AddModelError(string.Empty, result.Error);
            }

            return this.GridOperation(request, model);
        }

        [HttpPost]
        public JsonResult GetProblemsInProblemGroup([DataSourceRequest]DataSourceRequest request, int id)
        {
            var problems = this.problemGroupsData
                .GetProblemsById(id)
                .Select(DetailViewModelType.FromProblem);

            return this.Json(problems.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }

        private bool IsModelAndContestValid(ViewModelType model)
        {
            if (string.IsNullOrWhiteSpace(model.ContestName))
            {
                this.ModelState.AddModelError(nameof(model.ContestName), Resource.Contest_required);
                return false;
            }

            if (!this.contestsData.ExistsById(model.ContestId))
            {
                this.ModelState.AddModelError(nameof(model.ContestName), Resource.Contest_does_not_exist);
                return false;
            }

            if (!this.CheckIfUserHasContestPermissions(model.ContestId, this.contestsData))
            {
                this.ModelState.AddModelError(string.Empty, GeneralResource.No_privileges_message);
            }

            return this.ModelState.IsValid;
        }

        private ProblemGroup GetByIdAsNoTracking(int id) =>
            this.problemGroupsData.GetByIdQuery(id).AsNoTracking().SingleOrDefault();

        private void PrepareViewBagData() =>
            this.ViewBag.TypeData = DropdownViewModel.GetEnumValues<ProblemGroupType>();
    }
}