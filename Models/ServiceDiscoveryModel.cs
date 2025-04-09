namespace Manual_Ocelot.Models
{
    public class ServiceDiscoveryModel
    {
        public Guid ServiceId { get; set; }
        public Guid TenantId { get; set; }
        public string ServiceName { get; set; }
        public string Scheme { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public string HealthCheckUrl { get; set; }
    }
}
