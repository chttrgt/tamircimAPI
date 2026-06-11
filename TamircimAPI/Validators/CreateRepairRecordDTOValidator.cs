using FluentValidation;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Validators
{
    // Ücret (Price) için ortak kural: 0 veya pozitif, makul tavan, en fazla 2 ondalık.
    internal static class PriceRules
    {
        private const decimal MaxPrice = 9_999_999.99m;

        public static IRuleBuilderOptions<T, decimal?> ValidPrice<T>(this IRuleBuilder<T, decimal?> rule) =>
            rule
                .GreaterThanOrEqualTo(0).WithMessage("Ücret negatif olamaz.")
                .LessThanOrEqualTo(MaxPrice).WithMessage("Ücret çok yüksek.")
                .Must(p => !p.HasValue || decimal.Round(p.Value, 2) == p.Value)
                .WithMessage("Ücret en fazla 2 ondalık basamak içerebilir.");
    }

    public class CreateRepairRecordDTOValidator : AbstractValidator<CreateRepairRecordDTO>
    {
        public CreateRepairRecordDTOValidator()
        {
            RuleFor(x => x.DeviceId).GreaterThan(0).WithMessage("Geçerli bir cihaz seçiniz.");
            RuleFor(x => x.FaultDescription).NotEmpty().WithMessage("Arıza açıklaması zorunludur.");
            RuleFor(x => x.WorkDone).NotEmpty().When(x => x.Status == RepairStatus.Repaired)
                .WithMessage("Onarıldı durumunda yapılan işlemler zorunludur.");
            RuleFor(x => x.NotRepairedReason).NotEmpty().When(x => x.Status == RepairStatus.NotRepaired)
                .WithMessage("Onarılmadı durumunda sebep zorunludur.");
        }
    }

    public class SetPriceDTOValidator : AbstractValidator<SetPriceDTO>
    {
        public SetPriceDTOValidator()
        {
            RuleFor(x => x.Price).ValidPrice().When(x => x.Price.HasValue);
        }
    }

    public class UpdateRepairRecordDTOValidator : AbstractValidator<UpdateRepairRecordDTO>
    {
        public UpdateRepairRecordDTOValidator()
        {
            RuleFor(x => x.WorkDone).NotEmpty().When(x => x.Status == RepairStatus.Repaired)
                .WithMessage("Onarıldı durumunda yapılan işlemler zorunludur.");
            RuleFor(x => x.NotRepairedReason).NotEmpty().When(x => x.Status == RepairStatus.NotRepaired)
                .WithMessage("Onarılmadı durumunda sebep zorunludur.");
        }
    }
}
