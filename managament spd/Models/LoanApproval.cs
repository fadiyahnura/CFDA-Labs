using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementSPD.Models
{
    public class LoanApproval
    {
        [Key]
        public int ApprovalID { get; set; }

        public int TransactionID { get; set; }
        [ForeignKey("TransactionID")]
        public LoanTransaction LoanTransaction { get; set; }

        public int StaffID { get; set; }
        [ForeignKey("StaffID")]
        public User Staff { get; set; }

        [StringLength(50)]
        public string ApprovalStatus { get; set; } 

        public DateTime ApprovalDate { get; set; } = DateTime.Now;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(255)]
        public string Remarks { get; set; }
    }
}