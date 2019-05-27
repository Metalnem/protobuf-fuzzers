FROM ubuntu:trusty as cpp-env
WORKDIR /app

RUN apt-get update \
	&& apt-get install -y git wget \
	&& wget -qO - https://apt.llvm.org/llvm-snapshot.gpg.key | apt-key add - \
	&& echo 'deb http://apt.llvm.org/trusty/ llvm-toolchain-trusty main' >> /etc/apt/sources.list.d/llvm.list \
	&& echo 'deb-src http://apt.llvm.org/trusty/ llvm-toolchain-trusty main' >> /etc/apt/sources.list.d/llvm.list \
	&& echo 'deb http://ppa.launchpad.net/ubuntu-toolchain-r/test/ubuntu trusty main' >> /etc/apt/sources.list.d/llvm.list \
	&& gpg --keyserver keyserver.ubuntu.com --recv 1E9377A2BA9EF27F \
	&& gpg --export --armor 1E9377A2BA9EF27F | sudo apt-key add - \
	&& git clone https://github.com/google/libprotobuf-mutator.git \
	&& wget -qO - https://github.com/Kitware/CMake/releases/download/v3.14.2/cmake-3.14.2-Linux-x86_64.tar.gz | tar -xzC /opt

RUN apt-get update && apt-get install -y \
	binutils \
	clang-9 \
	dh-autoreconf \
	libfuzzer-9-dev \
	liblzma-dev \
	libz-dev \
	llvm-9 \
	llvm-9-dev \
	ninja-build \
	pkg-config

WORKDIR libprotobuf-mutator
COPY build/CMakeLists.txt build/http.proto build/libfuzzer-proto-dotnet.cc src/libfuzzer-proto-dotnet/
RUN echo 'add_subdirectory(libfuzzer-proto-dotnet)' >> src/CMakeLists.txt
WORKDIR build

RUN /opt/cmake-3.14.2-Linux-x86_64/bin/cmake .. -GNinja \
	-DCMAKE_BUILD_TYPE=Release \
	-DCMAKE_C_COMPILER=clang-9 \
	-DCMAKE_CXX_COMPILER=clang++-9 \
	-DLIB_PROTO_MUTATOR_DOWNLOAD_PROTOBUF=ON

RUN ninja

FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS csharp-env
WORKDIR /app

ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global SharpFuzz.CommandLine

WORKDIR /app/src/

COPY src/*.csproj ./
RUN dotnet restore -r linux-x64

COPY src/*.cs ./
COPY build/http.proto /app/build/

RUN dotnet publish -r linux-x64 --no-restore -c release -o out \
	&& sharpfuzz out/Microsoft.AspNetCore.Server.Kestrel.Core.dll -System.IO.Pipelines.DuplexPipe

FROM mcr.microsoft.com/dotnet/core/runtime:2.2
WORKDIR /app

COPY --from=cpp-env /app/libprotobuf-mutator/build/src/libfuzzer-proto-dotnet/libfuzzer-proto-dotnet ./
COPY --from=csharp-env /app/src/out ./
COPY corpus ./corpus

ENTRYPOINT ["./libfuzzer-proto-dotnet", "-timeout=5", "--target_path=./AspNetCore.Fuzz", "corpus"]
