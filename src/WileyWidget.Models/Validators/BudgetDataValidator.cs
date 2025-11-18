using FluentValidation;

namespace WileyWidget.Models.Validators
{
    public class BudgetDataValidator : AbstractValidator<BudgetData>
    {
        public BudgetDataValidator()
        {
            RuleFor(x => x.EnterpriseId).GreaterThan(0);
            RuleFor(x => x.FiscalYear).InclusiveBetween(2000, 2100);
            RuleFor(x => x.TotalBudget).GreaterThanOrEqualTo(0);
            RuleFor(x => x.TotalExpenditures).GreaterThanOrEqualTo(0);
            RuleFor(x => x.RemainingBudget)
                .Equal(x => x.TotalBudget - x.TotalExpenditures)
                .When(x => x.TotalBudget >= 0 && x.TotalExpenditures >= 0);
        }
    }
}
