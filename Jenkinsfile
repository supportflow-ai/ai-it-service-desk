// Jenkinsfile — AI IT Service Desk CI/CD Pipeline
// Branch strategy: feature/* → dev → staging → main
//
// Stage order (fail-fast: any failure stops the pipeline, no image push, no deploy):
//   Checkout → Restore → Build → Unit Tests → Architecture Tests
//   → Integration Tests → Frontend → Docker Build* → Health Check* → Smoke Test*
//   (* only on dev / staging / main branches)

pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO                = '1'
        // Write TRX results into the workspace so Jenkins can collect them
        TESTRESULTS                  = "${WORKSPACE}/artifacts/test-results"
    }

    stages {

        // ─── 1. Checkout ─────────────────────────────────────────────────
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        // ─── 2. Restore ──────────────────────────────────────────────────
        stage('Backend - Restore') {
            steps {
                dir('src/backend') {
                    sh 'dotnet restore ServiceDesk.sln'
                }
            }
        }

        // ─── 3. Build ────────────────────────────────────────────────────
        stage('Backend - Build') {
            steps {
                dir('src/backend') {
                    sh 'dotnet build ServiceDesk.sln -c Release --no-restore'
                }
            }
        }

        // ─── 4. Unit Tests ───────────────────────────────────────────────
        stage('Unit Tests') {
            steps {
                sh """
                    mkdir -p "${TESTRESULTS}/unit"
                    dotnet test tests/ServiceDesk.Domain.Tests/ServiceDesk.Domain.Tests.csproj \\
                        -c Release --no-build \\
                        --logger "trx;LogFileName=domain-tests.trx" \\
                        --results-directory "${TESTRESULTS}/unit"

                    dotnet test tests/ServiceDesk.Application.Tests/ServiceDesk.Application.Tests.csproj \\
                        -c Release --no-build \\
                        --logger "trx;LogFileName=application-tests.trx" \\
                        --results-directory "${TESTRESULTS}/unit"
                """
            }
            post {
                always {
                    // MSTest plugin reads TRX files
                    mstest testResultsFile: "artifacts/test-results/unit/*.trx",
                           keepLongStdio: true,
                           failOnError: true
                }
            }
        }

        // ─── 5. Architecture Tests ───────────────────────────────────────
        stage('Architecture Tests') {
            steps {
                sh """
                    mkdir -p "${TESTRESULTS}/architecture"
                    dotnet test tests/ServiceDesk.ArchitectureTests/ServiceDesk.ArchitectureTests.csproj \\
                        -c Release --no-build \\
                        --logger "trx;LogFileName=architecture-tests.trx" \\
                        --results-directory "${TESTRESULTS}/architecture"
                """
            }
            post {
                always {
                    mstest testResultsFile: "artifacts/test-results/architecture/*.trx",
                           keepLongStdio: true,
                           failOnError: true
                }
            }
        }

        // ─── 6. Integration Tests ────────────────────────────────────────
        // Uses Testcontainers (PostgreSQL) — requires Docker socket access.
        // TESTCONTAINERS_HOST_OVERRIDE is set to host.docker.internal when
        // running inside the Jenkins Docker container.
        stage('Integration Tests') {
            steps {
                sh """
                    mkdir -p "${TESTRESULTS}/integration"
                    dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj \\
                        -c Release --no-build \\
                        --logger "trx;LogFileName=integration-tests.trx" \\
                        --results-directory "${TESTRESULTS}/integration"
                """
            }
            post {
                always {
                    mstest testResultsFile: "artifacts/test-results/integration/*.trx",
                           keepLongStdio: true,
                           failOnError: true
                }
            }
        }

        // ─── 7. Frontend ─────────────────────────────────────────────────
        stage('Frontend - Install & Build') {
            steps {
                dir('src/frontend/service-desk-web') {
                    sh 'npm ci'
                    sh 'npm run build'
                }
            }
        }

        // ─── 8–10. Deployment stages (protected branches only) ───────────

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

        stage('Health Check') {
            when {
                anyOf {
                    branch 'dev'
                    branch 'staging'
                    branch 'main'
                }
            }
            steps {
                sh '''
                    echo "Waiting for services to become ready..."
                    sleep 20
                    HEALTH=$(curl --fail --silent http://localhost/health)
                    echo "$HEALTH"
                    echo "$HEALTH" | grep -q '"status":"Healthy"' || exit 1
                    echo "Health check PASSED."
                '''
            }
        }

        stage('Smoke Test') {
            when {
                anyOf {
                    branch 'dev'
                    branch 'staging'
                    branch 'main'
                }
            }
            steps {
                sh '''
                    curl --fail --silent --show-error http://localhost           && echo "Frontend: OK"
                    curl --fail --silent --show-error http://localhost/health    && echo "Health:   OK"
                    curl --fail --silent --show-error http://localhost/api       && echo "API Info: OK"
                    echo "All smoke checks passed."
                '''
            }
        }
    }

    // ─── Post-run ─────────────────────────────────────────────────────────
    post {
        always {
            // Collect all TRX results across all stages
            mstest testResultsFile: 'artifacts/test-results/**/*.trx',
                   keepLongStdio: true,
                   failOnError: false
        }
        failure {
            echo '━━━ Pipeline FAILED ━━━ Image was NOT pushed. Deployment was NOT triggered.'
        }
        success {
            echo '━━━ Pipeline PASSED ━━━ All stages completed successfully.'
        }
    }
}
