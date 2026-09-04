public static class DialogueEnum
{
    public enum AnswerTag { Correct, Wrong, ReallyBad }
    public enum QuestionType { Simple, Hard, Tricky }

    public enum DialogueOutcome
    {
        None,
        CaptureSuccess,
        CapturePartial,
        CaptureFail,
        CapturePenalty
    }
}
