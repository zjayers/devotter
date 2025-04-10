# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build/Test/Lint Commands
- Build: `npm run build` (or `yarn build`) 
- Dev server: `npm run dev` (or `yarn dev`)
- Lint: `npm run lint` (or `yarn lint`)
- Typecheck: `npm run typecheck` (or `yarn typecheck`) 
- Test all: `npm test` (or `yarn test`)
- Test single file: `npm test -- path/to/file.test.js` (or `yarn test path/to/file.test.js`)

## Code Style Guidelines
- **Formatting**: Use Prettier with default configuration
- **Imports**: Group and sort imports by type (built-in, external, internal)
- **Types**: Use TypeScript with strict mode enabled
- **Naming**: camelCase for variables/functions, PascalCase for classes/components
- **Components**: Use functional components with hooks (React)
- **Error Handling**: Use try/catch for async operations, proper error propagation
- **Documentation**: JSDoc for public APIs, comment complex logic
- **Testing**: Write unit tests for business logic and components