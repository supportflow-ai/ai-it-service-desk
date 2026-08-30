// Jenkinsfile — AI IT Service Desk CI/CD Pipeline
// Branch strategy: feature/* → dev → staging → main

pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Backend - Restore') {
            steps {
                dir('src/backend') {
                    sh 'dotnet restore ServiceDesk.sln'
                }
            }
        }

        stage('Backend - Build') {
            steps {
                dir('src/backend') {
                    sh 'dotnet build ServiceDesk.sln -c Release --no-restore'
                }
            }
        }

        stage('Backend - Unit Tests') {
            steps {
                dir('src/backend') {
                    sh 'dotnet test ServiceDesk.sln -c Release --no-build --logger "trx;LogFileName=test-results.trx" --filter "Category!=Integration"'
                }
            }
            post {
                always {
                    junit '**/TestResults/*.trx'
                }
            }
        }

        stage('Frontend - Install') {
            steps {
                dir('src/frontend/service-desk-web') {
                    sh 'npm ci'
                }
            }
        }

        stage('Frontend - Build') {
            steps {
                dir('src/frontend/service-desk-web') {
                    sh 'npm run build'
                }
            }
        }

        stage('Docker Build') {
            when {
                anyOf {
                    branch 'dev'
                    branch 'staging'
                    branch 'main'
                }
            }
            steps {
                sh 'docker compose build'
            }
        }

        // Future stages:
        // stage('Push to Registry') { ... }
        // stage('Deploy') { ... }
        // stage('Smoke Test') { ... }
    }

    post {
        failure {
            echo 'Pipeline failed — check logs above.'
        }
        success {
            echo 'Pipeline succeeded.'
        }
    }
}
