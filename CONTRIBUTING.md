# CONTRIBUTING

## Development workflow
- Create a feature branch from main.
- Keep changes focused and documented.
- Add or update tests for behavior changes.

## Backend
- Run `dotnet restore` from the backend folder.
- Run `dotnet test CALAC.sln` before opening a pull request.

## Frontend
- Run `npm install` and `npm run build` in the admin folder.

## Branching Strategy
- `main`: Production-ready code.
- `develop`: Integration branch for new features.
- `feature/*`: Specific feature development.
- `fix/*`: Bug fixes.

## Coding Standards
- **Versioning**: Follow `XX.XX.XX` semantic versioning. Update all relevant files on every release.
- **Documentation**: Keep `README.md`, `ROADMAP.md`, and `CHANGELOG.md` synchronized.
- **Backend**: Use File-scoped namespaces, Primary constructors where applicable, and XML comments for Public APIs.
- **Mobile**: Use MVVM pattern and ViewBinding in Kotlin modules.

## Documentation Workflow
Every significant change must be reflected in:
1. `CHANGELOG.md` (Added/Changed/Fixed/Removed)
2. `ROADMAP.md` (if part of a milestone)
3. Corresponding `.md` files in the `docs/` directory.
