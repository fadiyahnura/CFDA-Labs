using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementSPD.Models
{
    public class LoanTransaction
    {
        [Key]
        public int TransactionID { get; set; }

        public int LicenseID { get; set; }

        [ForeignKey("LicenseID")]
        public virtual License? License { get; set; }

        public int? EmployeeID { get; set; }

        [ForeignKey("EmployeeID")]
        public virtual User? Employee { get; set; }

        public int? StaffID { get; set; }

        [ForeignKey("StaffID")]
        public virtual User? Staff { get; set; }

        public DateTime RequestDate { get; set; } = DateTime.Now;
        public DateTime? ApprovalDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        public int Qty { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending";

        [StringLength(255)]
        public string Remarks { get; set; }

        public virtual ICollection<LoanApproval>? LoanApprovals { get; set; }
    }
}