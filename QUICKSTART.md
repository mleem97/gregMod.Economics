# Quickstart — gregMod.Economics

> Copy `gregMod.EconomyEngine.dll` to `Data Center/Mods/`. Press F11 to toggle the marketplace panel.

Repo: [https://github.com/mleem97/gregMod.Economics](https://github.com/mleem97/gregMod.Economics) · Version: `0.1.0` · License: Apache-2.0.

## 1. Clone

```bash
git clone https://github.com/mleem97/gregMod.Economics.git
cd gregMod.Economics
```

## 2. Build / Run

Depending on the tech stack, choose **one** path:

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

## 3. Test

```bash
dotnet test            # .NET
pnpm test              # Node
pytest                 # Python
```

Details are in [README.md](README.md) and [docs/INDEX.md](docs/INDEX.md).
If you run into problems: open an issue ([Issues](https://github.com/mleem97/gregMod.Economics/issues)) or read [CONTRIBUTING.md](CONTRIBUTING.md).
