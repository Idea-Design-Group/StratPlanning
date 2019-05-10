﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs
{
    public class PlanStepDTO
    {
        [Required]
        public int PlanId { get; set; }

        [Required]
        public string Step { get; set; }

        [Required]
        public IList<StepBlockDTO> StepBlocks { get; set; }

        public IEnumerable<UserPlanningMemberDTO> PlanningTeam { get; set; }

        public bool IsCompleted { get; set; }

        public bool IsAdmin { get; set; }

    }
}
