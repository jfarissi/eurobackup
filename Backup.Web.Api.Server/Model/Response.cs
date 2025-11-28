using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Model;

public partial class Response
{
    public int Id { get; set; }

    public int SurveyResponseId { get; set; }

    public int QuestionId { get; set; }

    public int RespondentId { get; set; }

    public string Answer { get; set; } = null!;

    public string? Option { get; set; }

    public int Itemid { get; set; }

    public virtual Respondent Respondent { get; set; } = null!;

    public virtual Surveyresponse SurveyResponse { get; set; } = null!;
}
