# Security Policy

## Supported versions

FPM is in `0.x` development, and only the latest release receives fixes. Please update to it before reporting a problem.

| Version | Supported |
|---|---|
| 0.1.x | Yes |

## Reporting a vulnerability

**Please do not report security problems in public issues, discussions or pull requests.**

Report them privately through GitHub instead: open the repository's **Security** tab and click **Report a vulnerability**. Include:

- what the problem is and where it is (type or member);
- the steps to reproduce it, with the platform and scripting backend if they matter;
- what an attacker could do with it.

You can expect a first answer within a week. Once the problem is confirmed, a fix is released as soon as possible, and you are credited in the release notes unless you prefer otherwise. Please keep the problem private until the fix is released.

## Scope

FPM's values often come from the network in multiplayer games. Examples of problems worth reporting:

- inputs (raw values, random generator states) that make FPM hang, crash, or read or write outside its tables;
- inputs that give different results on different machines, platforms or backends where the documentation promises the same, in a way a peer could exploit without the game noticing.

A result that is merely wrong, with no security impact, is a bug, not a vulnerability: please report it as a normal issue.
