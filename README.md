# fastreflect

C# reflection wrappers that provide portable, faster, and more natural APIs.

This library will soon power the reflection behind [Full Inspector](https://github.com/jacobdufault/fullinspector/) and [Full Serializer](https://github.com/jacobdufault/fullinspector/).

## Portable

Unity has many export platforms. Some of them, like Windows Metro, use an entirely different .NET profile and runtime, which uses different reflection APIs. FastReflect wraps these separate APIs behind a clean interface.

## Fast

FastReflect is designed to provide accelerated reflection operations, even on AOT-only .NET runtimes like il2cpp or mono AOT. This is accomplished by emitting C# files which are then included inside of the build process. FastReflect automatically discovers these auto-generated classes and uses them to eliminate all almost reflection.

If you're just doing a one-off reflection call, it is probably not worthwhile to use FastReflect, as there is going to be some (minimal) initialization overhead. The acceleration is only really useful when reflection calls are being run over and over again.

* Limitation: FastReflect can only accelerate publicly/internally accessible fields/members.

# Licensing

Freely available under the MIT license.
