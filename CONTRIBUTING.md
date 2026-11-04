# Contributing to Pulse

Contributions are welcome. Please read this page first: submitting one has legal consequences.

## Ownership of contributions

Pulse is licensed under the [GNU Affero General Public License v3.0](LICENSE). The copyright in the
project, and in every contribution accepted into it, belongs to Florian Rampin.

To contribute, you assign the copyright in your contribution to Florian Rampin under the
[Contributor Assignment Agreement](CLA.md). In particular, this lets Florian Rampin publish the
project, including your contribution, under other licenses, including proprietary ones.

You keep a license to reuse your own contribution, and your name stays in the Git history.

If you do not agree, you can still fork the project and change it under the terms of the AGPL. The agreement
only applies to what you submit to this repository.

## Submitting a pull request

1. Read [CLA.md](CLA.md).
2. Open your pull request and tick the declaration in its description. Each pull request needs
   its own declaration, and a pull request without it is not merged.
3. If your employer or a client may hold rights in your work, get their written permission first.
4. Name any third-party code in the pull request, with its source and license.

## Before you push

- Run the tests: `dotnet test PulseCore/tests/Noovio.Pulse.Tests`.
- Never commit a secret. Local credentials go in `dotnet user-secrets`, and production
  credentials stay on the server.

## Security issues

Please do not open a public issue for a security problem. Use the repository's
[private vulnerability reporting](https://github.com/noovio-software/Pulse/security/advisories/new).
