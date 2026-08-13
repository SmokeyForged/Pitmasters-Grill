using PitmastersGrill.Services;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class KillmailDbWriteGateShutdownTests
    {
        [Fact]
        public async Task WaitForIdleAsync_DoesNotCompleteWhileSharedWriterIsActive()
        {
            var writerGate = new KillmailDbWriteGate();
            var shutdownGate = new KillmailDbWriteGate();
            var writerLease = writerGate.Enter("shutdown regression writer");

            try
            {
                var barrierTask = shutdownGate.WaitForIdleAsync("application shutdown");

                await Task.Delay(150);
                Assert.False(barrierTask.IsCompleted);

                writerLease.Dispose();
                await barrierTask.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.True(barrierTask.IsCompletedSuccessfully);
            }
            finally
            {
                writerLease.Dispose();
            }
        }
    }
}
