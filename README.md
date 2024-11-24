# NUKE + Docker Image + GitHub Container Registry = ♥
![ci workflow status](https://github.com/SonnyRR/nuke-docker-github-registry/actions/workflows/ci.yml/badge.svg)

This is a simple project utilizing the NUKE automated build system and Docker that serves as an example of how to push Docker Images to the GitHub Container registry.

Check out NUKE:
https://github.com/nuke-build/nuke


## 💭 About
ℹ The sample API used in this repo is a `.NET 9 WebAPI` project utilizing the newly introduced `built-in container support`. 
I've also included a `dockerfile` which is also utilized in the `CI` pipeline. It's a magic 8-ball, that when prompted with a yes/no question will give you a random answer.

## 🏗 NUKE Build Project
The automated build project contains the necessary targets to `clean`, `restore`, `compile`, `build` and `publish` the docker images. 
You can view the target definitions in the `Build.cs` file and use it as a reference for your projects. 
It also contains a setup for `GitVersion` which lets us use semantic versioning when we tag the `git` commits & `docker` images.

## 📦 CI Pipeline
The artifacts produced by the `GitHub Actions` CI pipeline are two images with different tags. 
One of the images is built with the traditional multi-stage `Dockerfile`, while the other one utilizes the newly introduced `built-in` container support

You can view the whole pipeline config here: `.github/workflows/ci.yml` and use it as a reference for your projects.

## 🛠 Local Setup

### 🚢 Built-in container support
To build the docker image with the built-in container support, execute the following NUKE target:

```sh
# Global tool
nuke BuildApiImageWithBuiltInContainerSupport

# Shell script:
./build.sh BuildApiImageWithBuiltInContainerSupport
```

The aforementioned target will create a new image with the `built-in` tag: `magic-8-ball-api:built-in`.

❗ Only `Linux-x64` containers are supported with this approach.

### 🐳 Dockerfile
To build the docker image with the dockerfile, execute the following NUKE target:

```sh
# Global tool
nuke BuildApiImageWithDockerfile

# Shell script:
./build.sh BuildApiImageWithDockerfile
```

The aforementioned target will create a new image with the `dockerfile` tag: `magic-8-ball-api:dockerfile`.


## 🏃‍♀️ Run the containers
```sh
# Image built with built-in container support
docker run -d -p 5000:8080 --name m8b magic-8-ball-api:built-in

# Image built with dockerfile
docker run -d -p 5000:8080 --name m8b magic-8-ball-api:dockerfile
```

After that you can navigate to `http://localhost:5000` and it will redirect you to the `swagger` documentation for the `API`.

❗ I've intentionally not setup `HTTPS` redirection & not exposed the `443` port since that would require mounting additional volumes to access the development certifiacte. However feel free to update the `docker run` statement or a `docker-compose` file to mount those volumes & assign the necessary environment variables. You would also need to update the `Dockerfile` & API `.csproj` file to expose the ports.

To stop the container run the following command:
```sh
docker stop m8b

# To delete the container
docker rm m8b

# To delete the image
docker rmi magic-8-ball-api:dockerfile
docker rmi magic-8-ball-api:built-in 
```
