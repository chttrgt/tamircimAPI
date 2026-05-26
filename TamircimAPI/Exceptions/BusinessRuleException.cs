namespace TamircimAPI.Exceptions
{
    public class BusinessRuleException : Exception
    {
        public string RuleCode { get; }

        public BusinessRuleException(string message, string ruleCode = "BUSINESS_RULE_VIOLATION")
            : base(message)
        {
            RuleCode = ruleCode;
        }
    }
}
