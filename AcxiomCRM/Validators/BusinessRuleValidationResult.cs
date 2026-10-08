namespace AcxiomCRM.Validators;

public class BusinessRuleValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public Dictionary<string, List<string>> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddError(string propertyName, string errorMessage)
    {
        if (!Errors.TryGetValue(propertyName, out var list))
        {
            list = new List<string>();
            Errors[propertyName] = list;
        }
        list.Add(errorMessage);
    }
}
