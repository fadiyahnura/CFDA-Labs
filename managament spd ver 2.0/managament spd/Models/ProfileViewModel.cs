using System.ComponentModel.DataAnnotations;

namespace ManagementSPD.Models
{
    public class ProfileViewModel
    {
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Display(Name = "Role")]
        public string Role { get; set; }

        [Display(Name = "Contract ID")]
        public string? ContractID { get; set; }
    
        [Display(Name = "Employee No")]
        public string EmployeeNo { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Current Password (leave blank if you do not want to change)")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Konfirm New Password ")]
        [Compare("NewPassword", ErrorMessage = "The new password and the password confirmation do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}