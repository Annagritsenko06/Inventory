

# 1. Билд проекта
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файл проекта и восстанавливаем зависимости
COPY CourseWork/CourseWork/CourseWork.csproj ./CourseWork/
RUN dotnet restore ./CourseWork/CourseWork.csproj

# Копируем остальной код проекта
COPY CourseWork/CourseWork ./CourseWork

# Собираем проект в Release
RUN dotnet publish ./CourseWork/CourseWork.csproj -c Release -o /app

# 2. Финальный контейнер
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./


# 1. Билд проекта
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файл проекта и восстанавливаем зависимости
COPY CourseWork/CourseWork/CourseWork.csproj ./CourseWork/
RUN dotnet restore ./CourseWork/CourseWork.csproj

# Копируем остальной код проекта
COPY CourseWork/CourseWork ./CourseWork

# Собираем проект в Release
RUN dotnet publish ./CourseWork/CourseWork.csproj -c Release -o /app

# 2. Финальный контейнер
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Установка переменных окружения для email
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_SYSTEM_NET_MAIL_SMTPSERVER=smtp.gmail.com
ENV DOTNET_SYSTEM_NET_MAIL_SMTPPORT=587

# Запуск
ENTRYPOINT ["dotnet", "CourseWork.dll"]
