# Source and licence

The `.ids` and `.ifc` files in this folder are the official IDS implementer test cases,
copied unmodified from the buildingSMART IDS repository:

- Source: https://github.com/buildingSMART/IDS/tree/development/Documentation/ImplementersDocumentation/TestCases
- (c) buildingSMART International Ltd.
- Licensed under the Creative Commons Attribution-NoDerivatives 4.0 International License,
  https://creativecommons.org/licenses/by-nd/4.0/

The licence permits redistribution but not modification. Do not edit these files; to
update them, copy a newer release over them as a whole.

The file name prefix states the expected outcome: `pass-` and `fail-` cases are run by
`IdsOfficialTestCases`, `invalid-` cases test XSD validation of the IDS document itself and
are not run by the engine tests.
