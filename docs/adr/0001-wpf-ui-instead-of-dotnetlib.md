# WPF UI instead of dotnetlib

CodePrint asks WPF apps to take shared primitives and themes from `dotnetlib` first. Ping Runner is a
public repository, and `dotnetlib` is private, has no license yet, and is consumed through a local
NuGet feed that a public clone or GitHub's CI cannot reach. So Ping Runner uses WPF UI 4.3 from
nuget.org, as GoalMaker's Windows app does, and keeps its own semantic theme tokens. Revisit this when
`dotnetlib` publishes a licensed package to a public feed.
