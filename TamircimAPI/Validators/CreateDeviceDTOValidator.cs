using FluentValidation;
using TamircimAPI.Models.DTOs.Device;

namespace TamircimAPI.Validators
{
    public class CreateDeviceDTOValidator : AbstractValidator<CreateDeviceDTO>
    {
        public CreateDeviceDTOValidator()
        {
            RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("Geçerli bir müşteri seçiniz.");
            RuleFor(x => x.Brand).NotEmpty().MaximumLength(100).WithMessage("Marka zorunludur (max 100 karakter).");
            RuleFor(x => x.Model).NotEmpty().MaximumLength(200).WithMessage("Model zorunludur (max 200 karakter).");
            RuleFor(x => x.FaultDescription).NotEmpty().WithMessage("Arıza açıklaması zorunludur.");
        }
    }

    public class UpdateDeviceDTOValidator : AbstractValidator<UpdateDeviceDTO>
    {
        public UpdateDeviceDTOValidator()
        {
            RuleFor(x => x.Brand).NotEmpty().MaximumLength(100).WithMessage("Marka zorunludur (max 100 karakter).");
            RuleFor(x => x.Model).NotEmpty().MaximumLength(200).WithMessage("Model zorunludur (max 200 karakter).");
            RuleFor(x => x.FaultDescription).NotEmpty().WithMessage("Arıza açıklaması zorunludur.");
        }
    }
}
