# NUKE + Docker Image + GitHub Container Registry = ♥

![ci workflow status](https://github.com/SonnyRR/nuke-docker-github-registry/actions/workflows/ci.yml/badge.svg)

> [!WARNING]
> ⚠ This repository has been archived and is no longer maintained. NUKE is no longer actively maintained, so this project is preserved as-is for reference only — no further updates, bug fixes, or support will be provided.
>
> 🔁 NUKE users should migrate to [Fallout](https://github.com/Fallout-build/Fallout), the drop-in replacement for NUKE. See the [official migration guide](https://github.com/Fallout-build/Fallout/blob/main/docs/Migration/from-nuke.md) for step-by-step instructions (short version: `dotnet tool install -g Fallout.Migrate` and run `fallout-migrate` in your existing NUKE repo).

This is a simple project utilizing the NUKE automated build system and Docker that serves as an example of how to push Docker Images to the GitHub Container registry.

Check out NUKE:
https://github.com/nuke-build/nuke

## 💭 About

ℹ The sample API used in this repo is a `.NET 10 WebAPI` project built with a multi-stage Dockerfile. It's a magic 8-ball, that when prompted with a yes/no question will give you a random answer.

## 🏗 NUKE Build Project

The automated build project contains the necessary targets to `clean`, `restore`, `compile`, `build` and `publish` the docker image.
You can view the target definitions in the `Build.cs` file and use it as a reference for your projects.
It also contains a setup for `GitVersion` which lets us use semantic versioning when we tag the `git` commits & `docker` images.

## 📦 CI Pipeline

The artifact produced by the `GitHub Actions` CI pipeline is a single image built with a multi-stage Dockerfile using the Alpine base image.
The image is tagged with `latest` and the semantic version (e.g., `magic-8-ball-api-1.0.0`).

You can view the whole pipeline config here: `.github/workflows/ci.yml` and use it as a reference for your projects.

## 🛠 Local Setup

### 🐳 Dockerfile

To build the docker image with the Dockerfile, execute the following NUKE target:

```sh
# Global tool
nuke BuildApiImageWithDockerfile

# Shell script:
./build.sh BuildApiImageWithDockerfile
```

The aforementioned target will create a new image with the `latest` tag: `magic-8-ball-api:latest`.

## 🏃‍♀️ Run the container

```sh
docker run -d -p 5000:8080 --name m8b magic-8-ball-api:latest
```

After that you can navigate to `http://localhost:5000` and it will redirect you to the `swagger` documentation for the `API`.

❗ I've intentionally not setup `HTTPS` redirection & not exposed the `443` port since that would require mounting additional volumes to access the development certifiacte. However feel free to update the `docker run` statement or a `docker-compose` file to mount those volumes & assign the necessary environment variables. You would also need to update the `Dockerfile` & API `.csproj` file to expose the ports.

To stop the container run the following command:

```sh
docker stop m8b

# To delete the container
docker rm m8b

# To delete the image
docker rmi magic-8-ball-api:latest
```
