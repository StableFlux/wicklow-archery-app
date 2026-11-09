namespace WicklowArchery.Core.Forms;

public enum QuestionType
{
    Unknown = -1, ShortText = 0, Paragraph = 1, Radio = 2, Dropdown = 3, Checkbox = 4,
    Scale = 5, TextBlock = 6, Grid = 7, Section = 8, Date = 9, Time = 10, Image = 11, Video = 12, FileUpload = 13
}

public sealed record FormQuestion(long ItemId, string Title, string? Description, QuestionType Type,
    long? EntryId, bool Required, IReadOnlyList<string> Options);

public sealed record FormDefinition(string Title, Uri ResponseUrl, IReadOnlyList<FormQuestion> Questions,
    IReadOnlyList<KeyValuePair<string, string>> HiddenFields);
