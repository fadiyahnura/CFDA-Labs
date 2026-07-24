using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ManagementSPD.Models
{
    public class License
    {
        [Key]
        public int LicenseID { get; set; }

        [Required]
        [StringLength(100)]
        public string LicenseName { get; set; }

        [StringLength(50)]
        public string Category { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        public int TotalQuantity { get; set; } = 0;
        public int AvailableQuantity { get; set; } = 0;

        public ICollection<LoanTransaction> LoanTransactions { get; set; }
    }
}