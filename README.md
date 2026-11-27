# PulseMetrics

World of Warcraft tools at https://pulsemetrics.fr, in one application: the landing page at `/`, Alt (`/alt/`), Front (`/front/`), Life (`/life/`) and Burst (`/burst/`) run in a single process on a single port.

| Folder | Role |
|---|---|
| [`PulseMetrics/`](PulseMetrics/README.md) | The application: host, landing page, Alt, Front, Life and Burst, deployment |
| `PulseMetrics.Alt.Client/` | Alt's WebAssembly client (separate project, required by the WebAssembly SDK) |
| [`PulseCore/`](PulseCore/README.md) | Engine shared by Front, Life and Burst: domain, rules, API clients, pages, workers |
| [`NoovioPulseAddon/`](NoovioPulseAddon/README.md) | Companion World of Warcraft addon (`/importer`) |
| [`PulseAdmin/`](PulseAdmin/README.md) | Private usage statistics at https://admin.pulsemetrics.fr, deployed on its own |

## License

Copyright (C) 2026 Florian Rampin

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU Affero General Public License as published by the Free Software Foundation, version 3.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without
even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
[`LICENSE`](LICENSE) file for details.

SPDX-License-Identifier: `AGPL-3.0-only`

## Contributing

Contributions are welcome, and their copyright is assigned to Florian Rampin: every pull request
declares this assignment under the [Contributor Assignment Agreement](CLA.md). This only applies
to what is submitted to this repository, and forks stay under the AGPL. See
[`CONTRIBUTING.md`](CONTRIBUTING.md).

## Trademarks

The license grants no right to use the Noovio name or logos, other than to describe where the code
comes from.

World of Warcraft and Battle.net are trademarks of Blizzard Entertainment, Inc. This project is
not affiliated with or endorsed by Blizzard Entertainment or Raider.IO.
