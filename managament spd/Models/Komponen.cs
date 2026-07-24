
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementSPD.Models
{
    public class Komponen
    {
        [Key]
        [Column("No")]
        public int Id { get; set; }

        [Required(ErrorMessage = "License Name must be filled in.")]
        [StringLength(100, ErrorMessage = "The License Name must not exceed 100 characters..")]
        [Display(Name = "License Name")]
        [Column("LicenseName")]
        public string LicenseName { get; set; }

        [StringLength(50, ErrorMessage = "Contract ID must not exceed 50 characters..")]
        [Display(Name = "Contract ID")]
        [Column("ContractID")]
        public string? ContractID { get; set; }

        [Required(ErrorMessage = "Employee No must be filled in.")]
        [StringLength(50)]
        [Display(Name = "Employee No")]
        public string EmployeeNo { get; set; }

        [Required(ErrorMessage = "Quantity must be filled in.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
        [Column("Qty")]
        public int Qty { get; set; }

        [Required(ErrorMessage = "Start Date must be filled in.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        [Column("Start")]
        public DateTime Start { get; set; }

        [Required(ErrorMessage = "End Date must be filled in.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        [Column("End_lise")]   
        public DateTime End_lise { get; set; }

        [StringLength(50, ErrorMessage = "PIC must not exceed 50 characters.")]
        [Display(Name = "PIC")]
        [Column("PIC")]
        public string? PIC { get; set; }

        [Column("EmailSent")]
        public bool? EmailSent { get; set; }


        [Display(Name = "Status")]
        [Column("Status")]
        public string StatusApproval { get; set; } = "Pending";

        [NotMapped]
        public int RemainingDays
        {
            get
            {
                return (int)Math.Ceiling((End_lise - DateTime.Today).TotalDays);
            }
        }

        [NotMapped]
        public string Status
        {
            get
            {
                int remainingDays = RemainingDays;
                if (remainingDays <= 0)
                    return "Expired";
                else if (remainingDays <= 30)
                    return "Expiring Soon";
                else
                    return "Active";
            }
        }
    }
}
