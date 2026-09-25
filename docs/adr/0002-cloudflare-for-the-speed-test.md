# Cloudflare for the speed test

The speed test needs a public server that streams any number of bytes each way, answers close to the
user, and needs no key. Cloudflare's `speed.cloudflare.com` (`__down?bytes=N` and `__up`) does all
three and is what its own browser test uses; Ookla needs a licensed SDK, and fast.com's API is
undocumented. The cost is that results describe the path to Cloudflare's nearest data centre and depend
on an endpoint Cloudflare could change. `IThroughputEndpoint` keeps that in one adapter, so a second
server is one class and a registration.
