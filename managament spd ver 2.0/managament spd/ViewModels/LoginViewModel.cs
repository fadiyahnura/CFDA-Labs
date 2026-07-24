using System.ComponentModel.DataAnnotations;

namespace ManagementSPD.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Employee Number must be filled in.")]
        [Display(Name = "Employee Number")]
        public string EmployeeNo { get; set; }

        [Required(ErrorMessage = "Password must be filled in.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}