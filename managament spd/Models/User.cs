using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementSPD.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(255)]
        [Column("Password")]
        public string Password { get; set; }

        [Required]
        [StringLength(20)]
        public string Role { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(100)]
        [Display(Name = "Contract ID")]
        public string? ContractID { get; set; } 

        [Required]
        [StringLength(50)]
        [Display(Name = "Employee No")]
        public string EmployeeNo { get; set; }

        public virtual ICollection<Notification>? Notifications { get; set; }

        [InverseProperty("Employee")]
        public virtual ICollection<LoanTransaction>? LoansRequested { get; set; }

        [InverseProperty("Staff")]
        public virtual ICollection<LoanTransaction>? LoansManaged { get; set; }

        public virtual ICollection<LoanApproval>? ApprovalsHistory { get; set; }
    }
}