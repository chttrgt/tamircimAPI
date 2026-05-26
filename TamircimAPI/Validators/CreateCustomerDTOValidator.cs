using FluentValidation;
using TamircimAPI.Models.DTOs.Customer;

namespace TamircimAPI.Validators
{
    public class CreateCustomerDTOValidator : AbstractValidator<CreateCustomerDTO>
    {
        public CreateCustomerDTOValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("Ad zorunludur (max 100 karakter).");
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Soyad zorunludur (max 100 karakter).");
            RuleFor(x => x.Phone1).NotEmpty().MaximumLength(20).WithMessage("Cep telefonu zorunludur (max 20 karakter).");
            RuleFor(x => x.NationalId).MaximumLength(11).Matches(@"^\d{11}$").When(x => !string.IsNullOrEmpty(x.NationalId))
                .WithMessage("TC Kimlik No 11 haneli rakamdan oluşmalıdır.");
            RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("Geçerli bir e-posta adresi giriniz.");
        }
    }

    public class UpdateCustomerDTOValidator : AbstractValidator<UpdateCustomerDTO>
    {
        public UpdateCustomerDTOValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithMessage("Ad zorunludur (max 100 karakter).");
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithMessage("Soyad zorunludur (max 100 karakter).");
            RuleFor(x => x.Phone1).NotEmpty().MaximumLength(20).WithMessage("Cep telefonu zorunludur (max 20 karakter).");
            RuleFor(x => x.NationalId).MaximumLength(11).Matches(@"^\d{11}$").When(x => !string.IsNullOrEmpty(x.NationalId))
                .WithMessage("TC Kimlik No 11 haneli rakamdan oluşmalıdır.");
            RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
                .WithMessage("Geçerli bir e-posta adresi giriniz.");
        }
    }
}
