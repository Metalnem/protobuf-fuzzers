# Protobuf fuzzers

A collection of structure-aware fuzzers for [SharpFuzz] using
[libFuzzer] with custom mutations and [libprotobuf-mutator].
Work in progress.

[SharpFuzz]: https://github.com/Metalnem/sharpfuzz
[libFuzzer]: http://llvm.org/docs/LibFuzzer.html
[libprotobuf-mutator]: https://github.com/google/libprotobuf-mutator

## Projects

- [ASP.NET Core](https://github.com/Metalnem/protobuf-fuzzers/tree/master/AspNetCore)
- [Roslyn](https://github.com/Metalnem/protobuf-fuzzers/tree/master/Roslyn)

## Resources

- [Structure-Aware Fuzzing with libFuzzer](https://github.com/google/fuzzer-test-suite/blob/master/tutorial/structure-aware-fuzzing.md)
- [Getting Started with libprotobuf-mutator (LPM) in Chromium](https://chromium.googlesource.com/chromium/src/testing/libfuzzer/+/HEAD/libprotobuf-mutator.md)
- [Structure-aware fuzzing for Clang and LLVM with libprotobuf-mutator](https://www.youtube.com/watch?v=U60hC16HEDY)
