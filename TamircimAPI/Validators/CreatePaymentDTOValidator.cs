using FluentValidation;
using TamircimAPI.Models.DTOs.Payment;

namespace TamircimAPI.Validators
{
    public class CreatePaymentDTOValidator : AbstractValidator<CreatePaymentDTO>
    {
        // Üst sınır: numeric(12,2) → en fazla 10 tam basamak. Makul bir tavan koyup
        // hatalı/taşan girişleri engelliyoruz (örn. yanlışlıkla fazla sıfır).
        private const decimal MaxAmount = 9_999_999.99m;

        public CreatePaymentDTOValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Ödeme tutarı 0'dan büyük olmalıdır.")
                .LessThanOrEqualTo(MaxAmount).WithMessage("Ödeme tutarı çok yüksek.")
                // İki ondalıktan fazla hassasiyet kabul edilmez (kuruş bazında).
                .Must(a => decimal.Round(a, 2) == a).WithMessage("Tutar en fazla 2 ondalık basamak içerebilir.");

            RuleFor(x => x.Method).IsInEnum().WithMessage("Geçersiz ödeme yöntemi.");

            RuleFor(x => x.Note).MaximumLength(500).WithMessage("Not en fazla 500 karakter olabilir.");
        }
    }
}
