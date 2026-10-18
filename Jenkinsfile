pipeline {
    agent any
    stages {
        stage('Checkout') { steps { checkout scm } }
        stage('Restore') { steps { sh 'dotnet restore src/backend/ServiceDesk.sln --locked-mode' } }
        stage('Build') { steps { sh 'dotnet build src/backend/ServiceDesk.sln -c Release --no-restore' } }
        stage('Unit and Architecture') { steps { sh 'dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"' } }
        stage('PostgreSQL Integration') { steps { sh 'dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"' } }
        stage('AI Contract') {
            steps { sh 'python3 -m venv .venv && .venv/bin/pip install -r contracts/requirements.txt && .venv/bin/python scripts/validate_ai_contract.py' }
        }
        stage('Docker Images') {
            steps {
                sh 'docker build -f src/backend/ServiceDesk.Api/Dockerfile -t servicedesk-api:ci .'
                sh 'docker build -f src/backend/ServiceDesk.Worker/Dockerfile -t servicedesk-worker:ci .'
                sh 'docker build -f src/backend/ServiceDesk.Database/Dockerfile -t servicedesk-database:ci .'
            }
        }
    }
}
