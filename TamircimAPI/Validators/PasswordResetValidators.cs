using FluentValidation;
using TamircimAPI.Models.DTOs.Auth;

namespace TamircimAPI.Validators
{
    public class ForgotPasswordDTOValidator : AbstractValidator<ForgotPasswordDTO>
    {
        public ForgotPasswordDTOValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-posta adresi zorunludur.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
                .MaximumLength(100).WithMessage("E-posta en fazla 100 karakter olabilir.");
        }
    }

    public class ResetPasswordRequestDTOValidator : AbstractValidator<ResetPasswordRequestDTO>
    {
        public ResetPasswordRequestDTOValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("E-posta adresi zorunludur.")
                .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Kod zorunludur.")
                .Matches(@"^\d{6}$").WithMessage("Kod 6 haneli olmalıdır.");

            // Kayıt ile aynı şifre kuralları (tutarlılık).
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Şifre zorunludur.")
                .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
                .MaximumLength(100).WithMessage("Şifre en fazla 100 karakter olabilir.")
                .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
                .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.");
        }
    }
}
