using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementSPD.Models
{
    public class AuditLog
    {
        [Key]
        public int LogID { get; set; }

        public int? UserID { get; set; }

        [ForeignKey("UserID")]
        public User User { get; set; }

        public string Action { get; set; }

        public string TableName { get; set; }

        public string RecordID { get; set; }

        public string Details { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}