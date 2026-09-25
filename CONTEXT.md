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
The attempts of the latest run that the app keeps in memory, up to its capacity. The full run is in the
history.
_Avoid_: log

**History**:
Every run and speed test stored on this computer, kept until deleted. A stored run is a **run record**.
_Avoid_: log, archive

**Imported run**:
A stored run read from a file instead of recorded by pinging. It keeps the file's name; its interval
is the usual gap between its attempts.
_Avoid_: uploaded run, external run

**Report**:
A document about one set of attempts for someone who was not there: figures, findings, charts, tables
and how it was measured, as PDF or Excel.
_Avoid_: export (an export is the raw attempts), summary (the clipboard text)

**Finding**:
One sentence of a report that states what the attempts show, with the numbers, and judges no further.
_Avoid_: diagnosis, verdict

**Slice**:
One clock-aligned stretch of a report's period (a minute to a day, whatever keeps a report to 48 of
them) with its own figures. `ReportBucket` in code.
_Avoid_: interval (that is the gap between pings), window

**Interrupted run**:
A stored run the app never finished because it or the computer went down; closed at the next start
with the attempts that had been saved.
_Avoid_: crashed run, lost run

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
