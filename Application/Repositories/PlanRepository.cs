﻿using Core.Context;
using Core.Entities;
using System;
using Application.Interfaces.Repositories;
using Application.DTOs;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Core.Constants;
using AutoMapper;

namespace Application.Repositories
{
    public class PlanRepository : RepositoryBase<Plan>, IPlanRepository
    {
        public PlanRepository(PlanningDbContext context) : base(context)
        { }

        public bool AddUserToPlan(int userId, int planId, int positionId)
        {
            if (Context.UsersToPlans.Where(x => x.UserId == userId && x.PlanId == planId && x.Step == null).Any())
            {
                return false;
            }

            Context.UsersToPlans.Add(new UserToPlan { UserId = userId, PlanId = planId, PositionId = positionId });

            try
            {
                Save();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
        public bool AddUserToPlanPlanStep(int userId, int planId, string step)
        {
            if (Context.UsersToPlans.Where(x => x.UserId == userId && x.PlanId == planId && x.Step == step).Any())
            {
                return false;
            }

            Context.UsersToPlans.Add(new UserToPlan { UserId = userId, PlanId = planId, Step = step });

            try
            {
                Save();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool CreatePlan(PlanDTO plan, int userId)
        {
            var newPlan = new Plan
            {
                Name = plan.Name,
                Description = plan.Description,
                IsCompleted = false,
                StartDate = DateTime.Now,
                CreatedAt = DateTime.Now,
                CreatedBy = userId,
                UpdatedAt = DateTime.Now,
                UpdatedBy = userId
            };

            try
            {
                Context.Plans.Add(newPlan);
                Context.SaveChanges();

                var steps = this.GetStepList();
                foreach (var step in steps)
                {
                    GetOrCreateStepTask(newPlan.Id, step, userId);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public IEnumerable<PlanDTO> GetPlanList()
        {
            return FindAll().Select(p => Mapper.Map<PlanDTO>(p));
        }

        public IEnumerable<UserPlanningMemberDTO> GetPlanningTeam(int planId)
        {
            return Context.UsersToPlans
                .Where(x => x.PlanId == planId && x.Step == null).Include(x => x.User).Include(x => x.Position)
                .AsEnumerable()
                .Select(x => new UserPlanningMemberDTO { Id = x.User.Id, FullName = $"{x.User.FirstName} {x.User.LastName}", Position = x.Position?.Title })
                .ToList();
        }

        public PlanStepDTO GetStep(string stepIndex, int planId, bool isDefinitive, int userId)
        {
            //checks if a requested step is active in plan's tasks or just planid or/and stepindex are wrong
            var stepTask = GetStepTask(planId, stepIndex);

            if (stepTask == null)
            {
                return null;
            }

            var planStep = GetPlanStep(planId, stepIndex, isDefinitive);

            var otherUserStepResults = GetPlanStepResults(planId, stepIndex).Where(x => x.IsSubmitted).ToList();

            UserStepResult currentUserStepResult = GetOrCreateUserStepResult(planId, stepIndex, isDefinitive, userId);

            if (!isDefinitive)
            {
                var stepResult = otherUserStepResults.Where(x => x.UserToPlanId != null && x.UserToPlan.User.Id == userId).SingleOrDefault();
                otherUserStepResults.Remove(stepResult);

                var adminStepResult = GetSubmittedDefinitiveStepResult(planId, stepIndex);

                if (adminStepResult != null && adminStepResult.IsSubmitted)
                {
                    otherUserStepResults.Add(adminStepResult);
                }
            }

            planStep.IsSubmitted = currentUserStepResult.IsSubmitted;

            FillWithAnswers(planStep, currentUserStepResult, otherUserStepResults);

            return planStep;
        }

        public IEnumerable<string> GetStepList()
        {
            var members = typeof(Steps).GetMembers().Where(x => x.MemberType == System.Reflection.MemberTypes.Field);
            return members.Select(member => member.Name);
        }

        public bool RemoveUserFromPlan(int userId, int planId)
        {
            var userToPlan = Context.UsersToPlans.Where(x => x.UserId == userId && x.PlanId == planId).FirstOrDefault();

            if (userToPlan == null)
            {
                return false;
            }

            Context.UsersToPlans.Remove(userToPlan);

            try
            {
                Save();
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool SaveStep(PlanStepDTO planStep, bool isDefinitive, bool isSubmitted, int userId)
        {
            var stepTask = GetStepTask(planStep.PlanId, planStep.Step);

            if (stepTask == null)
            {
                return false;
            }

            if (!isDefinitive)
            {
                var userStepResult = GetOrCreateUserStepResult(planStep.PlanId, planStep.Step, false, userId);

                if (userStepResult.IsSubmitted)
                {
                    return false;
                }
                if (isSubmitted)
                {
                    userStepResult.IsSubmitted = true;
                }

                SaveAnswers(planStep.AnswerGroups, userStepResult);
            }
            else
            {
                var finalDefinitiveStepResult = GetFinalDefinitiveStepResult(planStep.PlanId, planStep.Step);
                if (finalDefinitiveStepResult == null)
                {
                    finalDefinitiveStepResult = CreateUserStepResult(planStep.PlanId, planStep.Step, isDefinitive, userId);
                    if (isSubmitted)
                    {
                        finalDefinitiveStepResult.IsSubmitted = true;
                    }
                }
                else
                {
                    var submittedDefinitiveResult = GetSubmittedDefinitiveStepResult(planStep.PlanId, planStep.Step);

                    if (isSubmitted)
                    {
                        if (!finalDefinitiveStepResult.IsSubmitted)
                        {
                            finalDefinitiveStepResult.IsSubmitted = true;

                            if (submittedDefinitiveResult != null)
                            {
                                submittedDefinitiveResult.IsSubmitted = false;
                            }
                        }
                    }
                    else
                    {
                        if (submittedDefinitiveResult != null)
                        {
                            if (submittedDefinitiveResult.Id == finalDefinitiveStepResult.Id)
                            {
                                var newFinalDefinitiveStepResult = CreateUserStepResult(planStep.PlanId, planStep.Step, isDefinitive, userId);
                                submittedDefinitiveResult.IsFinal = false;
                                finalDefinitiveStepResult = newFinalDefinitiveStepResult;
                            }
                        }
                    }
                }

                var otherDefinitiveResult = Context.UserStepResults.Where(x => x.IsFinal.HasValue && !x.IsFinal.Value && !x.IsSubmitted && x.IsDefinitive).SingleOrDefault();

                if (otherDefinitiveResult != null)
                {
                    DeleteUserStepResult(otherDefinitiveResult.Id);
                }

            }

            if (planStep.Step == Steps.Predeparture)
            {
                SaveStepTaskAnswers(planStep.StepTaskAnswers, planStep.PlanId, isDefinitive, userId);
            }

            Context.SaveChanges();
            return true;
        }

        public IEnumerable<PlanDTO> GetPlanListForUser(int userId)
        {
            return Context.UsersToPlans
                .Where(x => x.UserId == userId)
                .Include(x => x.Plan)
                .Select(x => Mapper.Map<PlanDTO>(x.Plan)).ToList();
        }

        public IList<FileDTO> GetFileAnswers(int questionId, int userId)
        {
            return Context.Questions.Where(x => x.Id == questionId)
                    .SelectMany(x => x.FileAnswers).Where(x => x.CreatedBy == userId)
                    .Select(x => Mapper.Map<FileDTO>(x.File)).ToList();
        }

        #region Private methods
        #region General methods

        private IList<StepTaskDTO> GetStepTasks(int planId)
        {
            return Context.StepTasks.Where(x => x.PlanId == planId).ToList().Select(x =>
            {
                StepTaskStatus status;
                if (x.IsCompleted)
                {
                    if (x.Schedule >= DateTime.Now)
                    {
                        status = StepTaskStatus.Completed;
                    }
                    else
                    {
                        status = StepTaskStatus.OverdueCompleted;
                    }
                }
                else
                {
                    if (x.Schedule >= DateTime.Now)
                    {
                        status = StepTaskStatus.Uncompleted;
                    }
                    else
                    {
                        status = StepTaskStatus.OverdueUnCompleted;
                    }
                }

                return new StepTaskDTO
                {
                    Id = x.Id,
                    PlanId = x.PlanId,
                    IsCompleted = x.IsCompleted,
                    RemindIn = x.Remind,
                    Schedule = x.Schedule,
                    Step = x.Step,
                    Status = status
                };
            }).ToList();
        }

        private UserStepResult GetOrCreateUserStepResult(int planId, string stepIndex, bool isDefinitive, int userId)
        {
            UserStepResult userStepResult;

            if (isDefinitive)
            {
                userStepResult = GetFinalDefinitiveStepResult(planId, stepIndex);
            }
            else
            {
                userStepResult = GetUserStepResult(planId, stepIndex, userId);
            }

            if (userStepResult == null)
            {
                userStepResult = CreateUserStepResult(planId, stepIndex, isDefinitive, userId);
            }

            return userStepResult;

        }

        private UserStepResult CreateUserStepResult(int planId, string stepIndex, bool isDefinitive, int userId)
        {
            var userStepResult = new UserStepResult
            {
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                CreatedBy = userId,
                UpdatedBy = userId,
                IsDefinitive = isDefinitive,
                PlanId = planId,
                Step = stepIndex
            };

            if (isDefinitive)
            {
                userStepResult.IsFinal = true;
            }
            else
            {
                userStepResult.PlanId = null;
                var userToPlan = Context.UsersToPlans.Where(x => x.UserId == userId && x.PlanId == planId).FirstOrDefault();
                userStepResult.UserToPlan = userToPlan;
            }

            Context.UserStepResults.Add(userStepResult);
            Context.SaveChanges();

            return userStepResult;
        }

        private UserStepResult GetUserStepResult(int planId, string stepIndex, int userId)
        {
            return Context.UserStepResults
                    .Where(x => x.Step == stepIndex)
                    .Include(x => x.UserToPlan).ThenInclude(x => x.User)
                    .Where(x => x.UserToPlan.UserId == userId && x.UserToPlan.PlanId == planId)
                    .Include(x => x.BooleanAnswers)
                    .Include(x => x.SelectAnswers).ThenInclude(x => x.Option)
                    .Include(x => x.TextAnswers)
                    .Include(x => x.FileAnswers).ThenInclude(x => x.File)
                    .Include(x => x.ValueAnswers)
                    .SingleOrDefault();
        }

        private UserStepResult GetSubmittedDefinitiveStepResult(int planId, string stepIndex)
        {
            return Context.UserStepResults
                    .Where(x => x.Step == stepIndex && x.PlanId == planId && x.IsDefinitive && x.IsSubmitted)
                    .Include(x => x.BooleanAnswers)
                    .Include(x => x.SelectAnswers).ThenInclude(x => x.Option)
                    .Include(x => x.TextAnswers)
                    .Include(x => x.FileAnswers).ThenInclude(x => x.File)
                    .Include(x => x.ValueAnswers)
                    .SingleOrDefault();
        }

        private UserStepResult GetFinalDefinitiveStepResult(int planId, string stepIndex)
        {
            return Context.UserStepResults
                    .Where(x => x.Step == stepIndex && x.PlanId == planId && x.IsDefinitive && x.IsFinal.HasValue && x.IsFinal.Value)
                    .Include(x => x.BooleanAnswers)
                    .Include(x => x.SelectAnswers).ThenInclude(x => x.Option)
                    .Include(x => x.TextAnswers)
                    .Include(x => x.FileAnswers).ThenInclude(x => x.File)
                    .Include(x => x.ValueAnswers)
                    .SingleOrDefault();
        }

        private IList<UserStepResult> GetPlanStepResults(int planId, string stepIndex)
        {
            var userToPlans = Context.UsersToPlans
               .Where(x => x.PlanId == planId)
               .ToList();

            var userStepResults = new List<UserStepResult>();

            foreach (var userToPlan in userToPlans)
            {
                var userStepResult = GetUserStepResult(planId, stepIndex, userToPlan.UserId);
                if (userStepResult != null)
                {
                    userStepResults.Add(userStepResult);
                }
            }

            return userStepResults;

        }

        private bool DeleteUserStepResult(int id)
        {
            var userStepResult = Context.UserStepResults.Where(x => x.Id == id)
                .Include(x => x.BooleanAnswers).Include(x => x.SelectAnswers).SingleOrDefault();

            if (userStepResult != null)
            {

                Context.BooleanAnswers.RemoveRange(userStepResult.BooleanAnswers);
                Context.SelectAnswers.RemoveRange(userStepResult.SelectAnswers);
                Context.UserStepResults.Remove(userStepResult);

                Context.SaveChanges();

                return true;
            }

            return false;
        }

        private IList<StepBlockDTO> GetStepStructure(string stepIndex)
        {
            return Context.StepBlocks.Where(x => x.Step == stepIndex)
                .Include(x => x.Questions)
                .ThenInclude(x => x.Options)
                .Include(x => x.Questions)
                .ThenInclude(x => x.Files)
                .OrderBy(x => x.Order)
                .AsEnumerable()
                .Select(x => Mapper.Map<StepBlockDTO>(x))
                .ToList();
        }

        private StepTask GetOrCreateStepTask(int planId, string stepIndex, int userId)
        {
            var stepTask = GetStepTask(planId, stepIndex);

            if (stepTask == null)
            {
                stepTask = new StepTask
                {
                    PlanId = planId,
                    Step = stepIndex,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    IsCompleted = false
                };

                Context.StepTasks.Add(stepTask);
                Context.SaveChanges();
            }

            return stepTask;
        }

        private StepTask GetStepTask(int planId, string stepIndex)
        {
            return Context.StepTasks.Where(x => x.PlanId == planId && x.Step == stepIndex).SingleOrDefault();
        }

        private PlanStepDTO GetPlanStep(int planId, string stepIndex, bool isDefinitive)
        {
            var steptask = GetStepTask(planId, stepIndex);

            var blocksDTOs = GetStepStructure(stepIndex);

            var planStep = new PlanStepDTO
            {
                PlanId = planId,
                Step = stepIndex,
                StepBlocks = blocksDTOs.ToList(),
                PlanningTeam = GetPlanningTeam(planId),
                IsAdmin = isDefinitive,
                IsCompleted = steptask?.IsCompleted ?? false,
                StepTasks = GetStepTasks(planId)
            };

            if (isDefinitive)
            {
                var involvedUsers = Context.UsersToPlans
                    .Where(x => x.PlanId == planId)
                    .Include(x => x.User);

                var userStepResults = Context.UserStepResults.Where(x => x.PlanId == planId && x.Step == stepIndex && !x.IsDefinitive).ToList();

                planStep.SubmittedUsers = involvedUsers.Where(involvedUser => userStepResults.Where(x => x.UserToPlanId == involvedUser.Id && x.IsSubmitted).Any())
                    .Select(x => new UserPlanningMemberDTO
                    {
                        FullName = $"{x.User.FirstName} {x.User.LastName}"
                    });

                planStep.NotSubmittedUsers = involvedUsers.Where(involvedUser => !userStepResults.Where(x => x.UserToPlanId == involvedUser.Id && x.IsSubmitted).Any())
                   .Select(x => new UserPlanningMemberDTO
                   {
                       FullName = $"{x.User.FirstName} {x.User.LastName}"
                   });
            }

            return planStep;
        }

        private void SaveStepTaskAnswers(AnswerGroupDTO answerGroup, int planId, bool isDefinitive, int userId)
        {
            List<StepTaskAnswer> dbStepTaskAnswers = null;
            UserToPlan currentUser = null;
            if (isDefinitive)
            {
                dbStepTaskAnswers = Context.StepTaskAnswers.Where(x => x.IsDefinitive && x.PlanId == planId).ToList();
            }
            else
            {
                currentUser = Context.UsersToPlans.Where(x => x.UserId == userId && x.PlanId == planId)
                   .Include(x => x.StepTaskAnswers).ThenInclude(x => x.StepTask).SingleOrDefault();

                dbStepTaskAnswers = currentUser?.StepTaskAnswers.ToList();
            }

            foreach (var dbStepTaskAnswer in dbStepTaskAnswers)
            {
                if (answerGroup?.Answer.StepTaskAnswers == null || !answerGroup.Answer.StepTaskAnswers.Any(x => x.Id == dbStepTaskAnswer.Id))
                {
                    Context.StepTaskAnswers.Remove(dbStepTaskAnswer);
                }
            }

            Context.SaveChanges();

            if (answerGroup?.Answer?.StepTaskAnswers == null)
            {
                return;
            }

            var stepTasks = Context.StepTasks.Where(x => x.PlanId == planId).ToList();

            var newStepTaskAnswers = new List<StepTaskAnswer>();

            foreach (var steptaskAnswer in answerGroup.Answer.StepTaskAnswers.Where(x => x.Id == 0))
            {
                var newAnswer = new StepTaskAnswer
                {
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                    Email = steptaskAnswer.Email,
                    FirstName = steptaskAnswer.FirstName,
                    LastName = steptaskAnswer.LastName,
                    StepTask = stepTasks.Where(x => x.Step == steptaskAnswer.Step).SingleOrDefault()
                };

                if (isDefinitive)
                {
                    newAnswer.PlanId = planId;
                    newAnswer.IsDefinitive = true;
                }
                else
                {
                    newAnswer.UserToPlan = currentUser;
                }

                newStepTaskAnswers.Add(newAnswer);
            }
            Context.StepTaskAnswers.AddRange(newStepTaskAnswers);
        }

        private AnswerGroupDTO GetStepTaskAnswers(int planId, bool isDefinitive, int userId)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO();

            List<StepTaskAnswer> currentUserStepTaskAnswers = null;

            if (isDefinitive)
            {
                currentUserStepTaskAnswers = Context.StepTaskAnswers.Where(x => x.IsDefinitive && x.PlanId == planId).Include(x => x.StepTask).ToList();
            }
            else
            {
                var currentUserToPlan = Context.UsersToPlans.Where(x => x.UserId == userId && x.PlanId == planId && x.Step == null)
                    .Include(x => x.StepTaskAnswers).ThenInclude(x => x.StepTask).SingleOrDefault();
                if (currentUserToPlan != null)
                {
                    currentUserStepTaskAnswers = currentUserToPlan.StepTaskAnswers.ToList();
                }
            }

            if (currentUserStepTaskAnswers != null)
            {
                answerGroup.Answer = new AnswerDTO
                {
                    StepTaskAnswers = currentUserStepTaskAnswers.Select(x => new StepTaskAnswerDTO
                    {
                        Id = x.Id,
                        Email = x.Email,
                        FirstName = x.FirstName,
                        LastName = x.LastName,
                        Step = x.StepTask.Step
                    }).ToList()
                };
            }

            List<StepTaskAnswer> definitiveStepTaskAnswers = Context.StepTaskAnswers.Where(x => x.IsDefinitive && x.PlanId == planId).Include(x => x.StepTask).ToList();

            if (definitiveStepTaskAnswers != null)
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO
                {
                    StepTaskAnswers = definitiveStepTaskAnswers.Select(x => new StepTaskAnswerDTO
                    {
                        Id = x.Id,
                        Email = x.Email,
                        FirstName = x.FirstName,
                        LastName = x.LastName,
                        Step = x.StepTask.Step
                    }).ToList()
                };
            }

            var otherUserToPlans = Context.UsersToPlans.Where(x => x.PlanId == planId && x.UserId != userId)
                .Include(x => x.User).Include(x => x.StepTaskAnswers).ThenInclude(x => x.StepTask).ToList();

            var otherStepTaskAnswers = new List<AnswerDTO>();

            foreach (var otherUserToPlan in otherUserToPlans)
            {
                var answerDTO = new AnswerDTO
                {
                    StepTaskAnswers = otherUserToPlan.StepTaskAnswers.Select(x => new StepTaskAnswerDTO
                    {
                        Email = x.Email,
                        FirstName = x.FirstName,
                        LastName = x.LastName,
                        Step = x.StepTask.Step
                    }).ToList(),
                    Author = $"{otherUserToPlan.User.FirstName} {otherUserToPlan.User.LastName}"
                };

                otherStepTaskAnswers.Add(answerDTO);
            }

            answerGroup.OtherAnswers = otherStepTaskAnswers;

            return answerGroup;
        }

        #endregion

        #region Saving methods

        private void SaveAnswers(IList<AnswerGroupDTO> answerGroups, UserStepResult userStepResult)
        {
            foreach (var answerGroup in answerGroups)
            {
                var question = Context.Questions.Where(x => x.Id == answerGroup.QuestionId).FirstOrDefault();

                if (question.Type == QuestionTypes.Boolean)
                {
                    SaveBooleanAnswer(answerGroup, userStepResult);
                }

                if (question.Type == QuestionTypes.Select)
                {
                    SaveSelectAnswer(answerGroup, userStepResult);
                }

                if (question.Type == QuestionTypes.TagMultiSelect)
                {
                    SaveTagMultiSelectAnswer(answerGroup, userStepResult);
                }
                if (question.Type == QuestionTypes.PlanTypeSelect)
                {
                    SaveBooleanAnswer(answerGroup, userStepResult);
                }

                if (question.Type == QuestionTypes.TextArea)
                {
                    SaveTextAnswer(answerGroup, userStepResult);
                }

                if (question.Type == QuestionTypes.File)
                {
                    SaveFileAnswer(answerGroup, userStepResult);
                }

                if (question.Type == QuestionTypes.Values)
                {
                    SaveValueAnswer(answerGroup, userStepResult);
                }
            }
        }

        private void SaveBooleanAnswer(AnswerGroupDTO answerGroup, UserStepResult userStepResult)
        {
            BooleanAnswer dbAnswer = null;

            dbAnswer = userStepResult.BooleanAnswers.Where(x => x.QuestionId == answerGroup.QuestionId).FirstOrDefault();

            if (dbAnswer != null)
            {
                if (dbAnswer.Answer != answerGroup.Answer.BooleanAnswer)
                {
                    dbAnswer.Answer = answerGroup.Answer.BooleanAnswer;
                    dbAnswer.UpdatedAt = DateTime.Now;
                    dbAnswer.UpdatedBy = userStepResult.UpdatedBy;
                }
            }
            else
            {
                var newAnswer = new BooleanAnswer
                {
                    Answer = answerGroup.Answer.BooleanAnswer,
                    QuestionId = answerGroup.QuestionId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = userStepResult.UpdatedBy,
                    UpdatedBy = userStepResult.UpdatedBy
                };

                userStepResult.BooleanAnswers.Add(newAnswer);
            }

        }

        private void SaveSelectAnswer(AnswerGroupDTO answerGroup, UserStepResult userStepResult)
        {
            SelectAnswer dbAnswer = null;

            dbAnswer = userStepResult.SelectAnswers.Where(x => x.QuestionId == answerGroup.QuestionId).FirstOrDefault();

            if (answerGroup.Answer.SelectAnswer.OptionId < 1)
            {
                answerGroup.Answer.SelectAnswer.OptionId = null;
            }

            if (answerGroup.Answer.SelectAnswer.OptionId != null)
            {
                answerGroup.Answer.SelectAnswer.AltOption = null;
            }

            if (dbAnswer != null)
            {
                if (!(dbAnswer.OptionId == answerGroup.Answer.SelectAnswer.OptionId
                    && dbAnswer.AltOption == answerGroup.Answer.SelectAnswer.AltOption))
                {
                    dbAnswer.OptionId = answerGroup.Answer.SelectAnswer.OptionId;
                    dbAnswer.AltOption = answerGroup.Answer.SelectAnswer.AltOption;
                    dbAnswer.UpdatedAt = DateTime.Now;
                    dbAnswer.UpdatedBy = userStepResult.UpdatedBy;
                }
            }
            else
            {
                var newAnswer = new SelectAnswer
                {
                    OptionId = answerGroup.Answer.SelectAnswer.OptionId,
                    AltOption = answerGroup.Answer.SelectAnswer.AltOption,
                    QuestionId = answerGroup.QuestionId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = userStepResult.UpdatedBy,
                    UpdatedBy = userStepResult.UpdatedBy
                };

                userStepResult.SelectAnswers.Add(newAnswer);
            }

        }

        private void SaveTagMultiSelectAnswer(AnswerGroupDTO answerGroup, UserStepResult userStepResult)
        {
            IList<SelectAnswer> dbAnswers = null;

            dbAnswers = userStepResult.SelectAnswers.Where(x => x.QuestionId == answerGroup.QuestionId).ToList();

            foreach (var dbAnswer in dbAnswers)
            {
                var answerText = dbAnswer.Option != null ? dbAnswer.Option.Title : dbAnswer.AltOption;

                if (!answerGroup.Answer.TagSelectAnswers.Contains(answerText))
                {
                    Context.SelectAnswers.Remove(dbAnswer);
                    Context.SaveChanges();
                    //userStepResult.SelectAnswers.Remove(dbAnswer);
                }
            }

            foreach (var answer in answerGroup.Answer.TagSelectAnswers)
            {
                if (!dbAnswers.Any(x => x.Option?.Title == answer || x.AltOption == answer))
                {
                    var dbOption = Context.Options.Where(x => x.QuestionId == answerGroup.QuestionId && x.Title == answer).FirstOrDefault();

                    var newAnswer = new SelectAnswer
                    {
                        QuestionId = answerGroup.QuestionId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedBy = userStepResult.UpdatedBy,
                        UpdatedBy = userStepResult.UpdatedBy
                    };

                    if (dbOption != null)
                    {
                        newAnswer.Option = dbOption;
                    }
                    else
                    {
                        newAnswer.AltOption = answer;
                    }

                    userStepResult.SelectAnswers.Add(newAnswer);
                }
            }
        }

        private void SaveTextAnswer(AnswerGroupDTO answerGroup, UserStepResult userStepResult)
        {
            TextAnswer dbAnswer = null;

            dbAnswer = userStepResult.TextAnswers.Where(x => x.QuestionId == answerGroup.QuestionId).FirstOrDefault();

            if (dbAnswer != null)
            {
                if (!dbAnswer.Text.Equals(answerGroup.Answer.TextAnswer.Text))
                {
                    dbAnswer.Text = answerGroup.Answer.TextAnswer.Text;
                    dbAnswer.UpdatedAt = DateTime.Now;
                    dbAnswer.UpdatedBy = userStepResult.UpdatedBy;
                }
            }
            else
            {
                var newAnswer = new TextAnswer
                {
                    Text = answerGroup.Answer.TextAnswer.Text,
                    QuestionId = answerGroup.QuestionId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = userStepResult.UpdatedBy,
                    UpdatedBy = userStepResult.UpdatedBy
                };

                userStepResult.TextAnswers.Add(newAnswer);
            }
        }

        private void SaveFileAnswer(AnswerGroupDTO answerGroup, UserStepResult userStepResult)
        {
            IList<FileAnswer> dbAnswers = null;

            dbAnswers = userStepResult.FileAnswers.Where(x => x.QuestionId == answerGroup.QuestionId).ToList();

            foreach (var dbAnswer in dbAnswers)
            {
                var answerFileId = dbAnswer.FileId;

                if (!answerGroup.Answer.InputFileAnswer.Contains(answerFileId))
                {
                    Context.FileAnswers.Remove(dbAnswer);
                    Context.SaveChanges();
                    //userStepResult.SelectAnswers.Remove(dbAnswer);
                }
            }

            foreach (var answer in answerGroup.Answer.InputFileAnswer)
            {
                if (!dbAnswers.Any(x => x.FileId == answer))
                {
                    var dbFile = Context.Files.Where(x => x.Id == answer).FirstOrDefault();

                    if (dbFile == null) continue;

                    var newAnswer = new FileAnswer
                    {
                        QuestionId = answerGroup.QuestionId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedBy = userStepResult.UpdatedBy,
                        UpdatedBy = userStepResult.UpdatedBy,
                        File = dbFile
                    };

                    userStepResult.FileAnswers.Add(newAnswer);
                }
            }
        }

        private void SaveValueAnswer(AnswerGroupDTO answerGroup, UserStepResult userStepResult)
        {
            IList<ValueAnswer> dbAnswers = null;

            dbAnswers = userStepResult.ValueAnswers.Where(x => x.QuestionId == answerGroup.QuestionId).ToList();

            foreach (var dbAnswer in dbAnswers)
            {
                var answerId = dbAnswer.Id;

                if (!answerGroup.Answer.ValueAnswer.Select(x => x.Id).Contains(answerId))
                {
                    Context.ValueAnswers.Remove(dbAnswer);
                    Context.SaveChanges();
                    //userStepResult.SelectAnswers.Remove(dbAnswer);
                }
            }

            foreach (var answer in answerGroup.Answer.ValueAnswer)
            {
                if (!dbAnswers.Any(x => x.Id == answer.Id))
                {
                    var newAnswer = new ValueAnswer
                    {
                        QuestionId = answerGroup.QuestionId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        CreatedBy = userStepResult.UpdatedBy,
                        UpdatedBy = userStepResult.UpdatedBy,
                        Value = answer.Value,
                        Definition = answer.Definition,
                        Description = answer.Description
                    };

                    userStepResult.ValueAnswers.Add(newAnswer);
                }
            }
        }

        #endregion

        #region Reading methods

        private void FillWithAnswers(PlanStepDTO planStep, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            planStep.AnswerGroups = new List<AnswerGroupDTO>();

            for (int i = 0; i < planStep.StepBlocks.Count; i++)
            {
                var questions = planStep.StepBlocks[i].Questions;

                for (int j = 0; j < questions.Count; j++)
                {
                    if (questions[j].Type == QuestionTypes.Boolean)
                    {
                        planStep.AnswerGroups.Add(GetBooleanAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }

                    if (questions[j].Type == QuestionTypes.Select)
                    {
                        planStep.AnswerGroups.Add(GetSelectAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }

                    if (questions[j].Type == QuestionTypes.TagMultiSelect)
                    {
                        planStep.AnswerGroups.Add(GetTagMultiSelectAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }

                    if (questions[j].Type == QuestionTypes.PlanTypeSelect)
                    {
                        planStep.AnswerGroups.Add(GetBooleanAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }
                    if (questions[j].Type == QuestionTypes.TextArea)
                    {
                        planStep.AnswerGroups.Add(GetTextAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }
                    if (questions[j].Type == QuestionTypes.File)
                    {
                        planStep.AnswerGroups.Add(GetFileAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }
                    if (questions[j].Type == QuestionTypes.Values)
                    {
                        planStep.AnswerGroups.Add(GetFileAnswers(questions[j].Id, currentUserStepResult, otherUserStepResults));
                    }
                }
            }

            if (planStep.Step == Steps.Predeparture)
            {
                planStep.StepTaskAnswers = GetStepTaskAnswers(planStep.PlanId, currentUserStepResult.IsDefinitive, currentUserStepResult.UserToPlan?.UserId ?? 0);
            }
        }

        private AnswerGroupDTO GetBooleanAnswers(int questionId, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO
            {
                QuestionId = questionId
            };

            var currentUserAnswer = currentUserStepResult.BooleanAnswers.Where(x => x.QuestionId == questionId).SingleOrDefault();

            if (currentUserAnswer != null)
            {
                answerGroup.Answer = new AnswerDTO { BooleanAnswer = currentUserAnswer.Answer };
            }

            var definitiveStepResult = otherUserStepResults.Where(x => x.IsDefinitive).SingleOrDefault();

            var definitiveAnswer = definitiveStepResult?.BooleanAnswers.Where(x => x.QuestionId == questionId).SingleOrDefault();

            if (definitiveAnswer != null)
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO { BooleanAnswer = definitiveAnswer.Answer };
            }

            var otherAnswers = new List<AnswerDTO>();

            foreach (var otherUserStepResult in otherUserStepResults.Where(x => !x.IsDefinitive))
            {
                var userAnswer = otherUserStepResult.BooleanAnswers.Where(a => a.QuestionId == questionId).SingleOrDefault();

                if (userAnswer != null)
                {
                    var answerDTO = new AnswerDTO
                    {
                        BooleanAnswer = userAnswer.Answer,
                        Author = $"{otherUserStepResult.UserToPlan.User.FirstName} {otherUserStepResult.UserToPlan.User.LastName}"
                    };

                    otherAnswers.Add(answerDTO);
                }
            }

            answerGroup.OtherAnswers = otherAnswers;

            return answerGroup;
        }


        private AnswerGroupDTO GetSelectAnswers(int questionId, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO
            {
                QuestionId = questionId
            };

            var currentUserAnswer = currentUserStepResult.SelectAnswers.Where(x => x.QuestionId == questionId).SingleOrDefault();

            if (currentUserAnswer != null)
            {
                answerGroup.Answer = new AnswerDTO
                {
                    SelectAnswer = new SelectAnswerDTO
                    {
                        OptionId = currentUserAnswer.OptionId,
                        AltOption = currentUserAnswer.AltOption
                    }
                };
            }

            var definitiveStepResult = otherUserStepResults.Where(x => x.IsDefinitive).SingleOrDefault();

            var definitiveAnswer = definitiveStepResult?.SelectAnswers.Where(x => x.QuestionId == questionId).SingleOrDefault();

            if (definitiveAnswer != null)
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO
                {
                    SelectAnswer = new SelectAnswerDTO
                    {
                        OptionId = definitiveAnswer.OptionId,
                        AltOption = definitiveAnswer.AltOption
                    }
                };
            }

            var otherAnswers = new List<AnswerDTO>();

            foreach (var otherUserStepResult in otherUserStepResults.Where(x => !x.IsDefinitive))
            {
                var userAnswer = otherUserStepResult.SelectAnswers.Where(a => a.QuestionId == questionId).SingleOrDefault();

                if (userAnswer != null)
                {
                    var answerDTO = new AnswerDTO
                    {
                        SelectAnswer = new SelectAnswerDTO
                        {
                            OptionId = userAnswer.OptionId,
                            AltOption = userAnswer.AltOption
                        },

                        Author = $"{otherUserStepResult.UserToPlan.User.FirstName} {otherUserStepResult.UserToPlan.User.LastName}"
                    };

                    otherAnswers.Add(answerDTO);
                }

            }

            answerGroup.OtherAnswers = otherAnswers;

            return answerGroup;
        }

        private AnswerGroupDTO GetTagMultiSelectAnswers(int questionId, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO
            {
                QuestionId = questionId
            };

            answerGroup.Answer = new AnswerDTO
            {
                TagSelectAnswers = currentUserStepResult
                                    .SelectAnswers
                                    .Where(x => x.QuestionId == questionId)
                                    .Select(x => x.OptionId != null ? x.Option.Title : x.AltOption)
                                    .ToList()
            };

            var definitiveStepResult = otherUserStepResults.Where(x => x.IsDefinitive).SingleOrDefault();

            if (definitiveStepResult != null)
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO
                {
                    TagSelectAnswers = definitiveStepResult
                                            .SelectAnswers
                                            .Where(x => x.QuestionId == questionId)
                                            .Select(x => x.OptionId != null ? x.Option.Title : x.AltOption)
                                            .ToList()
                };
            }

            var otherAnswers = new List<AnswerDTO>();

            foreach (var otherUserStepResult in otherUserStepResults.Where(x => !x.IsDefinitive))
            {
                var userAnswers = otherUserStepResult.SelectAnswers.Where(a => a.QuestionId == questionId).ToList();

                if (userAnswers.Any())
                {
                    var answerDTO = new AnswerDTO
                    {
                        TagSelectAnswers = userAnswers.Select(s => s.OptionId != null ? s.Option.Title : s.AltOption).ToList(),

                        Author = $"{otherUserStepResult.UserToPlan.User.FirstName} {otherUserStepResult.UserToPlan.User.LastName}"
                    };

                    otherAnswers.Add(answerDTO);
                }

            }

            answerGroup.OtherAnswers = otherAnswers;

            return answerGroup;
        }

        private AnswerGroupDTO GetTextAnswers(int questionId, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO
            {
                QuestionId = questionId
            };

            var currentUserAnswer = currentUserStepResult.TextAnswers.Where(x => x.QuestionId == questionId).SingleOrDefault();

            if (currentUserAnswer != null)
            {
                answerGroup.Answer = new AnswerDTO { TextAnswer = new TextAnswerDTO { IsIssue = currentUserAnswer.IsIssue, IsStakeholder = currentUserAnswer.IsStakeholder, Text = currentUserAnswer.Text } };
            }

            var definitiveStepResult = otherUserStepResults.Where(x => x.IsDefinitive).SingleOrDefault();

            var definitiveAnswer = definitiveStepResult?.TextAnswers.Where(x => x.QuestionId == questionId).SingleOrDefault();

            if (definitiveAnswer != null)
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO { TextAnswer = new TextAnswerDTO { IsIssue = definitiveAnswer.IsIssue, IsStakeholder = definitiveAnswer.IsStakeholder, Text = definitiveAnswer.Text } };
            }

            var otherAnswers = new List<AnswerDTO>();

            foreach (var otherUserStepResult in otherUserStepResults.Where(x => !x.IsDefinitive))
            {
                var userAnswer = otherUserStepResult.TextAnswers.Where(a => a.QuestionId == questionId).SingleOrDefault();

                if (userAnswer != null)
                {
                    var answerDTO = new AnswerDTO
                    {
                        TextAnswer = new TextAnswerDTO { IsIssue = userAnswer.IsIssue, IsStakeholder = userAnswer.IsStakeholder, Text = userAnswer.Text },
                        Author = $"{otherUserStepResult.UserToPlan.User.FirstName} {otherUserStepResult.UserToPlan.User.LastName}"
                    };

                    otherAnswers.Add(answerDTO);
                }
            }

            answerGroup.OtherAnswers = otherAnswers;

            return answerGroup;
        }

        private AnswerGroupDTO GetFileAnswers(int questionId, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO
            {
                QuestionId = questionId
            };

            var currentUserAnswer = currentUserStepResult.FileAnswers.Where(x => x.QuestionId == questionId);

            if (currentUserAnswer != null && currentUserAnswer.Any())
            {
                answerGroup.Answer = new AnswerDTO
                {
                    FileAnswer = currentUserAnswer.Select(x => new FileAnswerDTO
                    {
                        FileId = x.FileId,
                        Name = x.File?.Name,
                        Ext = x.File?.Ext,
                        Path = x.File?.Path
                    }).ToList()
                };
            }

            var definitiveStepResult = otherUserStepResults.Where(x => x.IsDefinitive).SingleOrDefault();

            var definitiveAnswer = definitiveStepResult?.FileAnswers.Where(x => x.QuestionId == questionId);

            if (definitiveAnswer != null && definitiveAnswer.Any())
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO
                {
                    FileAnswer = definitiveAnswer.Select(x => new FileAnswerDTO
                    {
                        FileId = x.FileId,
                        Name = x.File?.Name,
                        Ext = x.File?.Ext,
                        Path = x.File?.Path
                    }).ToList()
                };
            }

            var otherAnswers = new List<AnswerDTO>();

            foreach (var otherUserStepResult in otherUserStepResults.Where(x => !x.IsDefinitive))
            {
                var userAnswer = otherUserStepResult.FileAnswers.Where(x => x.QuestionId == questionId);

                if (userAnswer != null && userAnswer.Any())
                {
                    var answerDTO = new AnswerDTO
                    {
                        FileAnswer = userAnswer.Select(x => new FileAnswerDTO
                        {
                            FileId = x.FileId,
                            Name = x.File?.Name,
                            Ext = x.File?.Ext,
                            Path = x.File?.Path
                        }).ToList(),
                        Author = $"{otherUserStepResult.UserToPlan.User.FirstName} {otherUserStepResult.UserToPlan.User.LastName}"
                    };

                    otherAnswers.Add(answerDTO);
                }
            }

            answerGroup.OtherAnswers = otherAnswers;

            return answerGroup;
        }

        private AnswerGroupDTO GetValueAnswers(int questionId, UserStepResult currentUserStepResult, IList<UserStepResult> otherUserStepResults)
        {
            AnswerGroupDTO answerGroup = new AnswerGroupDTO
            {
                QuestionId = questionId
            };

            var currentUserAnswer = currentUserStepResult.ValueAnswers.Where(x => x.QuestionId == questionId);

            if (currentUserAnswer != null && currentUserAnswer.Any())
            {
                answerGroup.Answer = new AnswerDTO
                {
                    ValueAnswer = currentUserAnswer.Select(x => new ValueAnswerDTO
                    {
                        Id = x.Id,
                        Value = x.Value,
                        Definition = x.Definition,
                        Description = x.Description
                    }).ToList()
                };
            }

            var definitiveStepResult = otherUserStepResults.Where(x => x.IsDefinitive).SingleOrDefault();

            var definitiveAnswer = definitiveStepResult?.ValueAnswers.Where(x => x.QuestionId == questionId);

            if (definitiveAnswer != null && definitiveAnswer.Any())
            {
                answerGroup.DefinitiveAnswer = new AnswerDTO
                {
                    ValueAnswer = definitiveAnswer.Select(x => new ValueAnswerDTO
                    {
                        Id = x.Id,
                        Value = x.Value,
                        Definition = x.Definition,
                        Description = x.Description
                    }).ToList()
                };
            }

            var otherAnswers = new List<AnswerDTO>();

            foreach (var otherUserStepResult in otherUserStepResults.Where(x => !x.IsDefinitive))
            {
                var userAnswer = otherUserStepResult.ValueAnswers.Where(x => x.QuestionId == questionId);

                if (userAnswer != null && userAnswer.Any())
                {
                    var answerDTO = new AnswerDTO
                    {
                        ValueAnswer = userAnswer.Select(x => new ValueAnswerDTO
                        {
                            Id = x.Id,
                            Value = x.Value,
                            Definition = x.Definition,
                            Description = x.Description
                        }).ToList(),
                        Author = $"{otherUserStepResult.UserToPlan.User.FirstName} {otherUserStepResult.UserToPlan.User.LastName}"
                    };

                    otherAnswers.Add(answerDTO);
                }
            }

            answerGroup.OtherAnswers = otherAnswers;

            return answerGroup;
        }

        #endregion

        #endregion
    }
}
