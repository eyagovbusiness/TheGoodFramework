pipeline {
    agent {
        label 'imagechecker'
    }
    environment {
        REGISTRY='registry.guildswarm.org'
        IMAGE='the_good_framework'
        REPO='staging'
    }
    stages{
        stage('Build Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                    if (env.CHANGE_ID != null) {
                          def version = readFile('version').trim()
                          env.VERSION = version
                          sh''' find . \\( -name "*.csproj" -o -name "*.sln" -o -name "NuGet.docker.config" \\) -print0 \
                           | tar -cvf projectfiles.tar --null -T -
                           '''
						  try {
							sh "docker build . -t ${REGISTRY}/${REPO}/${IMAGE}:${version} -t ${REGISTRY}/${REPO}/${IMAGE}:latest"
						  } finally {
							    sh "rm -f projectfiles.tar"
							  }
                           }
                        }
                    }
                }
            }
        stage('Test Vulnerabilities'){
            steps{
                container('dockertainer'){
                    sh "trivy image --quiet ${REGISTRY}/${REPO}/${IMAGE}:latest"
                }
            }
        }
        stage('Push Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                        if (env.CHANGE_ID == null) { // this is for just the build once is passed
                                withCredentials([[$class: 'UsernamePasswordMultiBinding', credentialsId: 'harbor-base-images', usernameVariable: 'DOCKER_USERNAME', passwordVariable: 'DOCKER_PASSWORD']]) {
                                    sh "docker login -u \'${DOCKER_USERNAME}' -p $DOCKER_PASSWORD $REGISTRY"
                                    sh "docker push ${REGISTRY}/${REPO}/${IMAGE}:$version"
                                    sh "docker push ${REGISTRY}/${REPO}/${IMAGE}:latest"
                                    sh 'docker logout'
                                }
                            }
                        }
                    }
                }
            }
        stage('Remove Docker Images') {
            steps {
                script {
                    container ('dockertainer'){
                        if (env.CHANGE_ID == null) {
                            sh "docker rmi ${REGISTRY}/${REPO}/${IMAGE}:$version"
                            sh "docker rmi ${REGISTRY}/${REPO}/${IMAGE}:latest"
                            }
                        }
                    }
                }
            }
        }  
    post {
        always{
            sh 'rm -rf *'
        }
        failure {
            echo "Pipeline failed. Do any necessary cleanup here."
        }
    }
}