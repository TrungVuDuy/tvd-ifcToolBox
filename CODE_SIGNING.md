# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

Releases up to v2.0.0-preview.2 are not signed. Signing applies from the first release made after
the project is accepted by SignPath Foundation; the release notes say whether a release is signed.

## What is signed

Only the binaries built from this repository, by the [release workflow](.github/workflows/release.yml)
on GitHub-hosted runners, from the source of the release's tag:

- the desktop app: `IfcToolbox.exe` and `IfcToolbox*.dll`
- the command line tool: `ifctoolbox.exe`

Third-party files shipped beside them are not signed by this project. `IfcConvert_x64.exe` is
IfcOpenShell's, and the Microsoft, xbim and other libraries keep the signatures they came with. See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The files and the rules are in [.signpath/artifact-configuration.xml](.signpath/artifact-configuration.xml).

## Team roles

- Committers and reviewers: [TrungVuDuy](https://github.com/TrungVuDuy)
- Approvers: [TrungVuDuy](https://github.com/TrungVuDuy)

Every release signing request is approved by hand in SignPath.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically
requested by the user or the person installing or operating it.

Only the IFC Validator's bSDD option goes online, and only when it is used: it asks the
buildingSMART Data Dictionary (https://api.bsdd.buildingsmart.org) for its dictionaries, classes and
their properties. Nothing from the model is sent, and model files are never uploaded. The help and
project links open in the browser.

## Setting up signing (maintainers)

1. Make the repository public. SignPath Foundation signs open source projects only.
2. Apply at https://signpath.org/apply.html. The project must be released already, which it is.
3. Once accepted, in SignPath:
   - add the predefined *GitHub.com* trusted build system to the organization and link it to the
     project;
   - create the project with the slug `tvd-ifcToolBox`;
   - paste `.signpath/artifact-configuration.xml` into its artifact configuration;
   - use the signing policy `release-signing`.
4. In this repository, under Settings > Secrets and variables > Actions:
   - add the secret `SIGNPATH_API_TOKEN`: the API token of a SignPath user who may submit signing
     requests;
   - add the variable `SIGNPATH_ORGANIZATION_ID`;
   - add `SIGNPATH_PROJECT_SLUG` or `SIGNPATH_SIGNING_POLICY_SLUG` only if they differ from the
     slugs above.

The next tag then waits in the release workflow until the signing request is approved, for up to an
hour. Without `SIGNPATH_API_TOKEN` the workflow releases unsigned files, as before.
