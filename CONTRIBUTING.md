# Contributing to VCR

Thanks for your interest in contributing to VCR! This document provides guidelines and information for contributors.

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## How to Contribute

### Reporting Bugs

If you find a bug, please open an issue on GitHub with:

- A clear, descriptive title
- Steps to reproduce the issue
- Expected behavior vs. actual behavior
- Your environment (OS, .NET version, etc.)
- The `.tape` file that demonstrates the issue (if applicable)

### Suggesting Enhancements

Enhancement suggestions are welcome! Please open an issue with:

- A clear description of the enhancement
- Why this enhancement would be useful
- Examples of how it would work

### Pull Requests

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Make your changes
4. Ensure tests pass (`dotnet test`)
5. Ensure the project builds (`dotnet build`)
6. Commit your changes with clear, descriptive commit messages
7. Push to your fork
8. Open a Pull Request

## Development Setup

### Prerequisites

- .NET 9 SDK or later
- Git

### Building the Project

```bash
git clone https://github.com/phil-scott-78/vcr.git
cd vcr
dotnet build VcrSharp.sln
```

### Running Tests

```bash
dotnet test
```

### Project Structure

```
VcrSharp/
├── src/
│   ├── VcrSharp.Core/         # Core parsing and model
│   ├── VcrSharp.Infrastructure/ # Playwright integration
│   └── VcrSharp.Cli/           # CLI tool
├── tests/
│   ├── VcrSharp.Core.Tests/
│   ├── VcrSharp.Infrastructure.Tests/
│   └── VcrSharp.Cli.Tests/
└── samples/                    # Example .tape files
```

## Code Style

- Follow standard C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and concise

## Testing Guidelines

- Write unit tests for new functionality
- Ensure existing tests pass before submitting a PR
- Aim for good test coverage, especially for core parsing logic

## Commit Messages

- Use clear, descriptive commit messages
- Start with a verb in the imperative mood (e.g., "Add", "Fix", "Update")
- Keep the first line under 72 characters
- Add more detailed explanation in the commit body if needed

Examples:
- `Add support for RGB color values`
- `Fix race condition in shell execution`
- `Update documentation for Type command`

## Questions?

If you have questions about contributing, feel free to open an issue labeled "question".

## License

By contributing to VCR, you agree that your contributions will be licensed under the MIT License.
