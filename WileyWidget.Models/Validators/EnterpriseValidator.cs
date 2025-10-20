using FluentValidation;

namespace WileyWidget.Models.Validators
{
    public class EnterpriseValidator : AbstractValidator<Enterprise>
    {
        public EnterpriseValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.CurrentRate).GreaterThan(0);
            RuleFor(x => x.MonthlyExpenses).GreaterThanOrEqualTo(0);
            RuleFor(x => x.CitizenCount).GreaterThan(0);
        }
    }
}
