pipeline {
    agent { label 'windows' } 

    triggers {
        githubPush()
    }

    environment {
        APP_NAME = 'EnvanterServis'
        MSBUILD_PATH = '"C:\\Program Files (x86)\\Microsoft Visual Studio\\18\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe"'
        MINIO_PATH = 'C:\\Jenkins\\mc.exe'
        BUCKET_NAME = 'deployments'
    }
    stages {
        stage('Checkout') {
            steps {
                echo 'Proje SCM üzerinden alınıyor...'
                checkout scm
            }
        }
        stage('Determine & Validate Version') {
            steps {
                script {
                    def currentRemoteVersion = "1.0.0"
                    try {
                        currentRemoteVersion = bat(script: "@${MINIO_PATH} cat myminio/deployments/EnvanterServis/version.txt", returnStdout: true).trim()
                    } catch (Exception e) {
                        echo "MinIO'da henüz versiyon dosyası bulunamadı, 1.0.0'dan başlanıyor..."
                    }
            
                    def versionToLong = { v -> 
                        def parts = v.replaceAll("[^0-9.]", "").tokenize('.')
                        return parts[0].toInteger() * 10000 + parts[1].toInteger() * 100 + parts[2].toInteger()
                        }

                    long remoteVerNum = versionToLong(currentRemoteVersion)
                    String finalVer = ""

                    if (env.TAG_NAME) {
                        String taggedVer = env.TAG_NAME.replaceAll("[^0-9.]", "")
                        long taggedVerNum = versionToLong(taggedVer)

                        echo "Gelen Tag: ${taggedVer} (Değer: ${taggedVerNum})"
                        echo "MinIO'daki Mevcut: ${currentRemoteVersion} (Değer: ${remoteVerNum})"

                        if (taggedVerNum <= remoteVerNum) {
                            error "HATA! Gönderdiğin tag (${taggedVer}), mevcut sürümden (${currentRemoteVersion}) daha düşük veya eşit. Daha eski sürüm verilemez!"
                            }
                        finalVer = taggedVer
                        } 
                    else {
                        def parts = currentRemoteVersion.tokenize('.')
                        int major = parts[0].toInteger()
                        int minor = parts[1].toInteger()
                        int patch = parts[2].toInteger()
                        finalVer = "${major}.${minor}.${patch + 1}"
                        }

                    env.FINAL_VERSION = finalVer
                    echo "Onaylanan Yeni Versiyon: ${env.FINAL_VERSION}"
                }
            }
        }
        stage('Update Version') {
            steps {
                script {
                    def assemblyFile = "EnvanterServis\\Properties\\AssemblyInfo.cs"
                    echo "Versiyon güncelleniyor: ${env.FINAL_VERSION}"

                    bat """
                    powershell -command "(Get-Content ${assemblyFile}) -replace 'AssemblyVersion\\(\\\"[0-9.]+\\\"\\)', 'AssemblyVersion(\\\"${env.FINAL_VERSION}\\\")' | Set-Content ${assemblyFile}"
                    powershell -command "(Get-Content ${assemblyFile}) -replace 'AssemblyFileVersion\\(\\\"[0-9.]+\\\"\\)', 'AssemblyFileVersion(\\\"${env.FINAL_VERSION}\\\")' | Set-Content ${assemblyFile}"
                    """
                }
            }
        }
        stage('Build') {
            steps {
                script {
                    echo 'NuGet paketleri Solution üzerinden restore ediliyor...'
                    bat 'C:\\Windows\\nuget.exe restore EnvanterServis.sln'
                    
                    echo 'MSBuild ile derleme başlıyor...'
                    bat "${env.MSBUILD_PATH} EnvanterServis\\EnvanterServis.csproj /p:Configuration=Release /p:OutputPath=..\\publish"
                }
            }
        }
        stage('Zip & Deploy to MinIO') {
            steps {
                script {
                    def localDeployDir = "${WORKSPACE}\\myminio\\deployments\\${APP_NAME}"
                    bat "if not exist \"${localDeployDir}\" mkdir \"${localDeployDir}\""

                    def localZipPath = "${localDeployDir}\\${APP_NAME}-${env.FINAL_VERSION}.zip"
                    def localVerPath = "${localDeployDir}\\version.txt"
            
                    def publishPath = "publish\\app.publish\\" 
                    def bucketBase = "myminio/deployments/${APP_NAME}/"
                    
                    echo "Dosyalar şu geçici klasörde toplanıyor: ${localDeployDir}"

                    bat "if exist \"${localZipPath}\" del /f /q \"${localZipPath}\""
                    bat "powershell -command \"Compress-Archive -Path '${publishPath}*' -DestinationPath '${localZipPath}' -Force\""

                    bat "dir \"${localZipPath}\""

                    bat "echo ${env.FINAL_VERSION} > \"${localVerPath}\""

                    echo "MinIO'ya transfer başlıyor..."

                    bat "${MINIO_PATH} cp \"${localVerPath}\" ${bucketBase}version.txt"
                    bat "${MINIO_PATH} cp \"${localZipPath}\" ${bucketBase}v${env.FINAL_VERSION}/"
                    bat "${MINIO_PATH} cp \"${localZipPath}\" ${bucketBase}latest/${env.APP_NAME}.zip"
                    bat "if exist \"${localDeployDir}\" del /q /s \"${localDeployDir}\\*\""      
                }
            }
        }
    }
}