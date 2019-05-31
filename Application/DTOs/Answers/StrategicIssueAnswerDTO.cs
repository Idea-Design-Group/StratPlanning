﻿using Core.Constants;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs
{
    public class StrategicIssueAnswerDTO
    {
        public int IssueId { get; set; }

        public string Issue { get; set; }

        [MaxLength(EntityConfigs.TextAreaMaxLength)]
        public string Why { get; set; }

        [MaxLength(EntityConfigs.TextAreaMaxLength)]
        public string Result { get; set; }

        [MaxLength(EntityConfigs.TextAreaMaxLength)]
        public string Goal { get; set; }

        [MaxLength(EntityConfigs.TextAreaMaxLength)]
        public string Solution { get; set; }

        public int Ranking { get; set; }
    }
}
