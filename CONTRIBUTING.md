# Contributing to TSCloud

Thank you for your interest in contributing to TSCloud! We welcome contributions from the community and are grateful for your help in making TSCloud better.

## Ways to Contribute

### Bug Reports
- Search existing issues before creating a new one
- Use the bug report template when available
- Include detailed information about your environment
- Provide steps to reproduce the issue
- Include logs and screenshots when helpful

### Feature Requests
- Check existing feature requests to avoid duplicates
- Use the feature request template when available
- Explain the use case and why it's valuable
- Consider implementation complexity and alternatives
- Be open to discussion and feedback

### Code Contributions
- Fork the repository and create a feature branch
- Follow coding standards for each language
- Write tests for new functionality
- Update documentation as needed
- Submit a pull request with clear description

### Documentation
- Improve existing documentation for clarity
- Add examples and use cases
- Fix typos and formatting issues
- Translate documentation to other languages
- Create tutorials and guides

### Security
- Report security issues privately to security@tscloud.dev
- Do not create public issues for security vulnerabilities
- Follow responsible disclosure practices
- Allow time for fixes before public disclosure

## Getting Started

### Development Environment Setup

#### Prerequisites
- Git for version control
- Rust 1.70+ for core engine
- .NET 8.0 SDK for desktop application
- Node.js 18+ for web dashboard
- Android Studio for mobile application (optional)

#### Clone and Setup
```bash
# Clone the repository
git clone https://github.com/etrnkz/TSCloud.git
cd TSCloud

# Build Rust core
cd rust-core
cargo build
cargo test
cd ..

# Build desktop application
cd desktop-ui
dotnet restore
dotnet build
cd ..

# Build web dashboard (optional)
cd web-dashboard
npm install
npm run dev
cd ..
```

### Project Structure
```
TSCloud/
├── rust-core/          # Core encryption and networking (Rust)
├── desktop-ui/         # Windows desktop application (C#/WPF)
├── android-client/     # Android mobile application (Kotlin)
├── web-dashboard/      # Web dashboard (TypeScript/Next.js)
├── docs/              # Documentation and guides
└── .github/           # CI/CD workflows
```

## Coding Standards

### Rust (rust-core/)
- Follow rustfmt formatting standards
- Use clippy for linting and best practices
- Write comprehensive tests for all public APIs
- Document public functions with rustdoc comments
- Handle errors properly with Result types
- Use meaningful variable names and comments

```rust
/// Encrypts data using XChaCha20-Poly1305 with the provided key
/// 
/// # Arguments
/// * `data` - The plaintext data to encrypt
/// * `key` - The 32-byte encryption key
/// 
/// # Returns
/// * `Ok((encrypted_data, nonce))` on success
/// * `Err(CryptoError)` on failure
pub fn encrypt_data(data: &[u8], key: &[u8; 32]) -> Result<(Vec<u8>, [u8; 24]), CryptoError> {
    // Implementation here
}
```

### C# (desktop-ui/)
- Follow Microsoft C# conventions
- Use PascalCase for public members
- Use camelCase for private fields
- Add XML documentation for public APIs
- Use async/await for I/O operations
- Handle exceptions appropriately

```csharp
/// <summary>
/// Uploads a file to the configured Telegram channels with encryption
/// </summary>
/// <param name="filePath">Path to the file to upload</param>
/// <param name="isAutoSync">Whether this is an automatic sync operation</param>
/// <returns>Task representing the upload operation</returns>
public async Task UploadFileAsync(string filePath, bool isAutoSync = false)
{
    // Implementation here
}
```

### TypeScript (web-dashboard/)
- Use ESLint and Prettier for formatting
- Follow React/Next.js best practices
- Use TypeScript strictly with proper types
- Write JSDoc comments for complex functions
- Use meaningful component names
- Implement proper error handling

```typescript
/**
 * Uploads a file to TSCloud with progress tracking
 * @param file - The file to upload
 * @param onProgress - Progress callback function
 * @returns Promise resolving to upload result
 */
export async function uploadFile(
  file: File,
  onProgress?: (progress: number) => void
): Promise<UploadResult> {
  // Implementation here
}
```

### Kotlin (android-client/)
- Follow Android Kotlin style guide
- Use meaningful class and function names
- Implement proper error handling
- Use coroutines for async operations
- Follow MVVM architecture patterns
- Add KDoc comments for public APIs

```kotlin
/**
 * Uploads a file to TSCloud with encryption
 * @param uri The URI of the file to upload
 * @param onProgress Progress callback
 * @return Flow emitting upload progress and result
 */
suspend fun uploadFile(
    uri: Uri,
    onProgress: (Float) -> Unit = {}
): Flow<UploadState> {
    // Implementation here
}
```

## Testing Guidelines

### Unit Tests
- Write tests for all new functionality
- Test edge cases and error conditions
- Use descriptive test names that explain what is being tested
- Mock external dependencies appropriately
- Aim for high code coverage (>80%)

### Integration Tests
- Test complete workflows end-to-end
- Use test data that doesn't affect production
- Clean up test resources after tests complete
- Test cross-platform compatibility when applicable

### Security Tests
- Test encryption/decryption with known test vectors
- Verify key derivation with standard test cases
- Test error handling for security-related failures
- Validate input sanitization and bounds checking

## Pull Request Process

### Before Submitting
1. Create a feature branch from `main`
2. Make your changes following coding standards
3. Write or update tests as needed
4. Update documentation if required
5. Run all tests and ensure they pass
6. Check code formatting and linting

### Pull Request Template
```markdown
## Description
Brief description of changes made.

## Type of Change
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing completed

## Checklist
- [ ] Code follows project style guidelines
- [ ] Self-review of code completed
- [ ] Code is commented, particularly in hard-to-understand areas
- [ ] Documentation updated as needed
- [ ] No new warnings introduced
```

### Review Process
1. Automated checks must pass (CI/CD)
2. Code review by at least one maintainer
3. Security review for security-related changes
4. Testing on multiple platforms when applicable
5. Documentation review for user-facing changes

## Issue Labels

We use labels to categorize and prioritize issues:

### Type Labels
- `bug` - Something isn't working correctly
- `enhancement` - New feature or improvement
- `documentation` - Documentation related
- `security` - Security-related issue
- `performance` - Performance improvement
- `refactor` - Code refactoring

### Priority Labels
- `priority: critical` - Critical issues requiring immediate attention
- `priority: high` - High priority issues
- `priority: medium` - Medium priority issues
- `priority: low` - Low priority issues

### Component Labels
- `component: rust-core` - Rust core engine
- `component: desktop` - Desktop application
- `component: android` - Android application
- `component: web` - Web dashboard
- `component: docs` - Documentation

## Development Workflow

### Branching Strategy
- **main** - Stable release branch
- **develop** - Development integration branch (if used)
- **feature/*** - Feature development branches
- **bugfix/*** - Bug fix branches
- **hotfix/*** - Critical hotfix branches

### Commit Messages
Follow conventional commit format:
```
type(scope): description

[optional body]

[optional footer]
```

Examples:
```
feat(desktop): add file versioning support
fix(rust-core): resolve encryption key derivation issue
docs(readme): update installation instructions
```

## Security Guidelines

### Reporting Security Issues
- Email security@tscloud.dev for security issues
- Include detailed information about the vulnerability
- Allow reasonable time for fixes before disclosure
- Follow responsible disclosure practices

### Security Best Practices
- Never commit secrets or credentials
- Use secure coding practices for all languages
- Validate all inputs and sanitize outputs
- Follow principle of least privilege
- Keep dependencies updated and scan for vulnerabilities

### Cryptographic Guidelines
- Use established libraries for cryptographic operations
- Never implement custom crypto without expert review
- Use secure random number generation
- Follow current best practices for key management
- Test with known test vectors when available

## Getting Help

### Community Support
- GitHub Discussions - General questions and discussions
- GitHub Issues - Bug reports and feature requests

### Documentation
- README.md - Project overview and quick start
- docs/ - Detailed documentation and guides
- API Documentation - Generated from code comments

## Recognition

### Contributors
All contributors are recognized in:
- GitHub contributors page
- Release notes for significant contributions

### Types of Contributions
We value all types of contributions:
- Code contributions - New features and bug fixes
- Documentation - Guides, tutorials, and API docs
- Testing - Bug reports and quality assurance
- Design - UI/UX improvements and assets
- Community - Support and outreach
- Translation - Internationalization support

## License

By contributing to TSCloud, you agree that your contributions will be licensed under the same MIT License that covers the project. See the [LICENSE](LICENSE) file for details.

---

Thank you for contributing to TSCloud! Together, we're building a secure and private cloud storage solution.