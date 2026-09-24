# Quickstart — gregMod.Economics

> Copy `gregMod.EconomyEngine.dll` to `Data Center/Mods/`. Press F11 to toggle the marketplace panel.

Repo: [https://github.com/mleem97/gregMod.Economics](https://github.com/mleem97/gregMod.Economics) · Version: `0.1.0` · Lizenz: Apache-2.0.

## 1. Klonen

```bash
git clone https://github.com/mleem97/gregMod.Economics.git
cd gregMod.Economics
```

## 2. Bauen / Starten

Je nach Tech-Stack **einen** Weg wählen:

```bash
# .NET
dotnet build -c Release
dotnet run --project src/

# Node / pnpm
pnpm install
pnpm build
pnpm start

# Python
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
python -m <modul>
```

## 3. Testen

```bash
dotnet test            # .NET
pnpm test              # Node
pytest                 # Python
```

Details stehen in [README.md](README.md) und [docs/INDEX.md](docs/INDEX.md).
Bei Problemen: Issue anlegen ([Issues](https://github.com/mleem97/gregMod.Economics/issues)) oder [CONTRIBUTING.md](CONTRIBUTING.md) lesen.
