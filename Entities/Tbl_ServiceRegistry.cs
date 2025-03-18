using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Manual_Ocelot.Entities
{
    [Table("Tbl_ServiceRegistry")]
    public class Tbl_ServiceRegistry
    {
        [Key]
        public string ServiceId { get; set; }
        public string ServiceName { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string HealthCheckEndpoint { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
