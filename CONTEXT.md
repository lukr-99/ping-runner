# Ping Runner

Measuring one internet connection from one Windows PC: how fast and steady replies are, how many go
missing, and how much the line can carry.

## Language

**Attempt**:
One echo request and its outcome: a reply with a round-trip time, or a failure with a reason.
_Avoid_: ping result, sample (a sample is one number)

**Run**:
Pinging one target on a fixed interval until a set duration passes or someone stops it.
_Avoid_: job, test

**Session**:
The attempts of the latest run that the app keeps in memory, up to its capacity.
_Avoid_: log, history

**Latency**:
The round-trip time of a reply, in milliseconds.
_Avoid_: ping (as a number), delay

**Jitter**:
The mean change in latency between consecutive replies. Lost attempts in between are skipped.
_Avoid_: variance, standard deviation (reported separately as σ)

**Packet loss**:
Failed attempts as a share of all attempts in the range being looked at.
_Avoid_: drop rate

**Outage**:
Two or more failed attempts in a row. It starts at the first failure and ends at the next reply, or at
the latest failure while it is still going on.
_Avoid_: downtime (implies the whole line), disconnect

**Call quality**:
A mean opinion score from 1 to 4.5 estimated from latency, jitter and loss with the simplified E-model.
_Avoid_: MOS as a measurement; it is an estimate

**Throughput**:
Bits per second moved in one direction during a speed test. The average and the peak both leave out
the first second; the peak is the best rate held across one full second.
_Avoid_: bandwidth, speed (except in "speed test")

**Loaded latency**:
Latency to the speed-test latency host while a transfer is running.
_Avoid_: latency under load (fine in the UI, but use this term in code)

**Bufferbloat**:
How much the median loaded latency rises over the idle median, graded A+ to F.
_Avoid_: lag
