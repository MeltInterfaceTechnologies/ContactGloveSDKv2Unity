using System;
using System.Net;

namespace MeltInterfaceTechnologies.ContactGlove.V2
{
    [Serializable]
    public sealed class HostOptions
    {
        public string HostId = "localhost";
        public string DisplayName = "DivingStation";
        public string IpAddress = "127.0.0.1";
        public int ServerPort = 25791;
        public int ReceivePort = 25788;
        public int SendPort;
        public int NotificationCapacity = 256;
        /// <summary>0 disables local stale diagnosis. No measured hardware default is asserted.</summary>
        public double StaleAfterSeconds;
        public double ProfileTimeoutSeconds = 5;
        internal bool TryValidate(out IPAddress address)
        {
            return IPAddress.TryParse(IpAddress, out address) && !string.IsNullOrWhiteSpace(HostId)
                && ServerPort > 0 && ServerPort <= 65535 && ReceivePort >= 0 && ReceivePort <= 65535 && SendPort >= 0 && SendPort <= 65535
                && NotificationCapacity >= 8 && NotificationCapacity <= 65536
                && !double.IsNaN(StaleAfterSeconds) && !double.IsInfinity(StaleAfterSeconds) && StaleAfterSeconds >= 0
                && !double.IsNaN(ProfileTimeoutSeconds) && !double.IsInfinity(ProfileTimeoutSeconds) && ProfileTimeoutSeconds > 0;
        }
        internal HostOptions Copy() => (HostOptions)MemberwiseClone();
    }
    public sealed class SessionDiagnostics
    {
        public string HostId { get; internal set; }
        public SessionState State { get; internal set; }
        public int ReceivePort { get; internal set; }
        public int SendPort { get; internal set; }
        public int QueueDepth { get; internal set; }
        public long ReceivedDatagrams { get; internal set; }
        public long AcceptedRecords { get; internal set; }
        public long ParseErrors { get; internal set; }
        public long CacheLimitDrops { get; internal set; }
        public long NotificationDrops { get; internal set; }
        public long CallbackExceptions { get; internal set; }
        public long UnexpectedSources { get; internal set; }
        public long UnmatchedProfileResponses { get; internal set; }
        public long ConnectSends { get; internal set; }
        public double LastReceivedAt { get; internal set; }
        public string LastError { get; internal set; }
        public bool InputResynchronizationRequired { get; internal set; }
    }
    public sealed class NotificationLoss
    {
        public string HostId { get; }
        public long DroppedCount { get; }
        /// <summary>Release latched input and re-read snapshots; lost edges cannot be reconstructed.</summary>
        internal NotificationLoss(string host, long count) { HostId = host; DroppedCount = count; }
    }
    /// <summary>Uses absolute monotonic deadlines. Resume causes one registration, without a burst.</summary>
    public sealed class HeartbeatSchedule
    {
        private double last = double.NegativeInfinity;
        public bool IsDue(double now) => now - last >= 30;
        public bool CanRetry(double now) => now - last >= 1;
        public void MarkSent(double now) { last = now; }
    }
}
