FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

COPY . /app

RUN apt-get update &&  apt-get install -y libsodium-dev ffmpeg &&  rm -rf /var/lib/apt/lists/*
    
RUN chmod 777 ./HidamariBot.dll

ENTRYPOINT ["dotnet", "HidamariBot.dll"]