using FluentValidation;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Validators
{
    public class CreateRepairRecordDTOValidator : AbstractValidator<CreateRepairRecordDTO>
    {
        public CreateRepairRecordDTOValidator()
        {
            RuleFor(x => x.DeviceId).GreaterThan(0).WithMessage("Geçerli bir cihaz seçiniz.");
            RuleFor(x => x.WorkDone).NotEmpty().When(x => x.Status == RepairStatus.Repaired)
                .WithMessage("Onarıldı durumunda yapılan işlemler zorunludur.");
            RuleFor(x => x.NotRepairedReason).NotEmpty().When(x => x.Status == RepairStatus.NotRepaired)
                .WithMessage("Onarılmadı durumunda sebep zorunludur.");
            RuleFor(x => x.WaitingReason).NotEmpty().When(x => x.Status == RepairStatus.Waiting)
                .WithMessage("Beklemede durumunda bekleme sebebi zorunludur.");
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
            RuleFor(x => x.WaitingReason).NotEmpty().When(x => x.Status == RepairStatus.Waiting)
                .WithMessage("Beklemede durumunda bekleme sebebi zorunludur.");
        }
    }
}
