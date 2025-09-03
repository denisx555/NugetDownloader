# NugetDownloader

## English

A console utility for downloading NuGet packages listed in a `Directory.Packages.props` file from multiple NuGet repositories.

### Features

-   Parses `Directory.Packages.props` to get a list of packages and versions.
-   Downloads packages from multiple specified NuGet repositories.
-   Supports different URL structures for nuget.org (flat container API) and other repositories like Nexus.
-   Skips downloading packages that already exist in the output directory.
-   Allows disabling SSL certificate validation for repositories with self-signed certificates.
-   Supports authentication (username/password) for private repositories.
-   Provides color-coded console output for better readability (Green for success, Red for failure).
-   Downloads packages in parallel to speed up the process.

### Usage

The application is run from the command line.

```sh
dotnet run -- [options]
```

#### Arguments

| Argument                 | Description                                                 | Required |
| ------------------------ | ----------------------------------------------------------- | -------- |
| `--props-path <path>`    | Path to the `Directory.Packages.props` file.                | Yes      |
| `--output-dir <path>`    | Local directory to save the downloaded `.nupkg` files.      | Yes      |
| `--sources <urls>`       | A single, comma-separated string of NuGet repository URLs.  | Yes      |
| `--disable-ssl-validation <bool>` | Disable SSL certificate validation (`true` or `false`). | No       |
| `--user <username>`      | Username for the private repository.                        | No       |
| `--password <password>`  | Password for the private repository.                        | No       |

### Example

```sh
dotnet run -- --props-path "C:\projects\MySolution\Directory.Packages.props" --output-dir "C:\nuget-offline-cache" --sources "https://api.nuget.org/v3-flatcontainer,https://nexus.mycompany.com/repository/private/" --disable-ssl-validation true --user "myuser" --password "mypassword"
```

---

## Русский

Консольная утилита для скачивания NuGet-пакетов, перечисленных в файле `Directory.Packages.props`, из нескольких NuGet-репозиториев.

### Возможности

-   Читает `Directory.Packages.props` для получения списка пакетов и их версий.
-   Скачивает пакеты из нескольких указанных NuGet-репозиториев.
-   Поддерживает различные форматы URL для nuget.org (flat container API) и других репозиториев (например, Nexus).
-   Пропускает скачивание пакетов, которые уже существуют в выходной директории.
-   Позволяет отключать проверку SSL-сертификата для репозиториев с самоподписанными сертификатами.
-   Поддерживает аутентификацию (имя пользователя/пароль) для приватных репозиториев.
-   Обеспечивает цветной вывод в консоль для лучшей читаемости (зеленый - успех, красный - ошибка).
-   Скачивает пакеты параллельно для ускорения процесса.

### Использование

Приложение запускается из командной строки.

```sh
dotnet run -- [аргументы]
```

#### Аргументы

| Аргумент                 | Описание                                                    | Обязательный |
| ------------------------ | ----------------------------------------------------------- | ------------ |
| `--props-path <путь>`    | Путь к файлу `Directory.Packages.props`                    | Да           |
| `--output-dir <путь>`    | Локальная директория для сохранения скачанных `.nupkg` файлов. | Да           |
| `--sources <url-адреса>` | Одна строка с URL-адресами NuGet-репозиториев, разделенными запятой. | Да           |
| `--disable-ssl-validation <bool>` | Отключить проверку SSL-сертификата (`true` или `false`). | Нет          |
| `--user <имя>`           | Имя пользователя для приватного репозитория.                | Нет          |
| `--password <пароль>`    | Пароль для приватного репозитория.                          | Нет          |

### Пример

```sh
dotnet run -- --props-path "C:\projects\MySolution\Directory.Packages.props" --output-dir "C:\nuget-offline-cache" --sources "https://api.nuget.org/v3-flatcontainer,https://nexus.mycompany.com/repository/private/" --disable-ssl-validation true --user "myuser" --password "mypassword"
```
