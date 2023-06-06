# LivestreamRecorderBackend

This is the backend azure function for the [Recorder.moe](https://recorder.moe) livestream recorder project.

## Settings

### Hosting on Azure Functions

- Fork this repository.
- Set up github repository action secrets and follow the [github workflow](./.github/workflows/azure-functions-app-dotnet.yml).

### Docker image

- Use this Docker image `ghcr.io/recorder-moe/livestreamrecorderbackend`

> **Warning**
> To set up all the environment variables, please follow the docker-compose example provided in the [docker-compose.yml](./docker-compose.yml) file.
